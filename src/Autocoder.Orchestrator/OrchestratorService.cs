using System.Diagnostics;
using System.Text.Json;
using Autocoder.Core.Data;
using Autocoder.Core.Enums;
using Autocoder.Core.Interfaces;
using Autocoder.Core.Models;
using Autocoder.Core.Orchestration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Autocoder.Orchestrator;

public class OrchestratorService : IOrchestrator
{
    private readonly AutocoderDbContext _db;
    private readonly IAgentRunner _agentRunner;
    private readonly IGitService _gitService;
    private readonly PromptBuilder _promptBuilder;
    private readonly IRunningTaskRegistry _registry;
    private readonly ILogger<OrchestratorService> _logger;

    public OrchestratorService(
        AutocoderDbContext db,
        IAgentRunner agentRunner,
        IGitService gitService,
        PromptBuilder promptBuilder,
        IRunningTaskRegistry registry,
        ILogger<OrchestratorService> logger)
    {
        _db = db;
        _agentRunner = agentRunner;
        _gitService = gitService;
        _promptBuilder = promptBuilder;
        _registry = registry;
        _logger = logger;
    }

    public async Task ProcessTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await _db.WorkTasks
            .Include(t => t.Board)
                .ThenInclude(b => b.Columns)
                    .ThenInclude(c => c.ShellCommands)
            .Include(t => t.Board)
                .ThenInclude(b => b.Repositories)
            .Include(t => t.ContextEntries.OrderBy(e => e.CreatedAt))
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new InvalidOperationException($"Task {taskId} not found.");

        if (task.Status != WorkTaskStatus.Waiting && task.Status != WorkTaskStatus.Asking)
            throw new InvalidOperationException(
                $"Task cannot be processed in status '{task.Status}'. Must be Waiting or Asking.");

        var column = task.Board.Columns.FirstOrDefault(c => c.Id == task.CurrentColumnId)
            ?? throw new InvalidOperationException($"Column {task.CurrentColumnId} not found on board.");

        if (column.Type != ColumnType.Agent)
            throw new InvalidOperationException($"Column '{column.Name}' is not an agent column.");

        if (!column.AgentEnabled && column.ShellCommands.Count == 0)
        {
            throw new InvalidOperationException(
                $"Column '{column.Name}' has AgentEnabled=false but no shell commands configured.");
        }

        task.Status = WorkTaskStatus.Running;
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var orderedColumns = task.Board.Columns.OrderBy(c => c.Position).ToList();

        // Pre-create branch/worktree on first run (before any agent commits)
        if (task.BranchName is null && task.Board.Repositories.Count > 0)
        {
            task.BranchName = GenerateBranchName(task.Title);
            await _gitService.SetupWorktreeAsync(task, ct);
            task.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        // ── Phase 1: Pre shell commands ──────────────────────────────────────
        var preCommands = column.ShellCommands
            .Where(c => c.Phase == ShellCommandPhase.Pre)
            .OrderBy(c => c.Position)
            .ToList();

        if (preCommands.Count > 0)
            await RunShellCommandsAsync(task, column, preCommands, ct);

        // ── Phase 2: Worker (optional) ───────────────────────────────────────
        string workerOutput = string.Empty;

        if (column.AgentEnabled)
        {
            var workerPrompt = _promptBuilder.Build(task, column, orderedColumns);

            AgentResult workerResult;
            using var workerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (column.TimeoutSeconds > 0)
                workerCts.CancelAfter(TimeSpan.FromSeconds(column.TimeoutSeconds));

            try
            {
                workerResult = await _agentRunner.RunAsync(workerPrompt, workerCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                await SetErrorAsync(task, $"Worker agent timed out after {column.TimeoutSeconds} seconds.", ct);
                return;
            }

            if (workerResult.TimedOut)
            {
                await SetErrorAsync(task, $"Worker agent timed out after {column.TimeoutSeconds} seconds.", ct);
                return;
            }

            if (IsRateLimited(workerResult.FullOutput))
            {
                await SetErrorAsync(task, "Claude API rate limited. Try again later.", ct);
                return;
            }

            workerOutput = workerResult.FullOutput;
        }

        // ── Phase 3: Post shell commands (optional) ──────────────────────────
        string? shellSummary = null;
        List<ShellCommandResult>? shellResults = null;

        var postCommands = column.ShellCommands
            .Where(c => c.Phase == ShellCommandPhase.Post)
            .OrderBy(c => c.Position)
            .ToList();

        if (postCommands.Count > 0)
        {
            shellResults = await RunShellCommandsAsync(task, column, postCommands, ct);
            shellSummary = await SummarizeShellResultsAsync(task, column, shellResults, ct);
        }

        // ── Phase 3: Determiner ──────────────────────────────────────────────
        var determinerPrompt = _promptBuilder.BuildDeterminer(task, column, workerOutput, shellSummary);

        AgentResult determinerResult;
        using var determinerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        determinerCts.CancelAfter(TimeSpan.FromSeconds(120));

        try
        {
            determinerResult = await _agentRunner.RunAsync(determinerPrompt, determinerCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            await SetErrorAsync(task, "Determiner agent timed out.", ct);
            return;
        }

        // Save work output now, before the Determiner check, so it's preserved even if routing fails
        ContextEntry? workerEntry = null;
        if (column.AgentEnabled && !string.IsNullOrWhiteSpace(workerOutput))
        {
            workerEntry = new ContextEntry
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                Kind = ContextEntryKind.AgentOutput,
                ColumnId = column.Id,
                ColumnName = column.Name,
                Content = workerOutput,
                CreatedAt = DateTime.UtcNow
            };
            _db.ContextEntries.Add(workerEntry);
            await _db.SaveChangesAsync(ct);
        }

        if (IsRateLimited(determinerResult.FullOutput))
        {
            await SetErrorAsync(task, "Claude API rate limited. Try again later.", ct);
            return;
        }

        if (determinerResult.StructuredOutput is null)
        {
            var snippet = determinerResult.FullOutput.Length > 3000
                ? determinerResult.FullOutput[..3000] + "\n...[truncated]"
                : determinerResult.FullOutput;
            _logger.LogError(
                "Determiner parse failed for task {TaskId}. Output ({Len} chars):\n{Output}",
                taskId, determinerResult.FullOutput.Length, snippet);
            await SetErrorAsync(task, "Determiner did not produce valid structured output for routing.", ct);
            return;
        }

        var output = determinerResult.StructuredOutput;

        // Back-fill routing metadata onto the worker entry now that we have it
        if (workerEntry is not null)
        {
            workerEntry.StructuredData = output.RawJson;
            workerEntry.Action = output.Action;
        }

        // Save shell output entry (if shell commands ran)
        if (shellResults is not null && shellSummary is not null)
        {
            _db.ContextEntries.Add(new ContextEntry
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                Kind = ContextEntryKind.ShellOutput,
                ColumnId = column.Id,
                ColumnName = column.Name,
                Content = shellSummary,
                StructuredData = JsonSerializer.Serialize(shellResults),
                // In shell-only columns, store the routing action here so history is complete
                Action = column.AgentEnabled ? null : output.Action,
                CreatedAt = DateTime.UtcNow
            });
        }

        var currentIdx = orderedColumns.FindIndex(c => c.Id == column.Id);

        switch (output.Action)
        {
            case TransitionAction.Forward:
                var isLast = currentIdx >= orderedColumns.Count - 1;
                if (isLast)
                {
                    task.Status = WorkTaskStatus.Done;
                }
                else
                {
                    var next = orderedColumns[currentIdx + 1];
                    task.CurrentColumnId = next.Id;
                    if (column.AutoForward)
                        task.Status = next.Type == ColumnType.Input ? WorkTaskStatus.Done : WorkTaskStatus.Waiting;
                    else
                        task.Status = WorkTaskStatus.PendingApproval;
                }
                task.PendingQuestion = null;
                break;

            case TransitionAction.Backward:
                var backTarget = column.BackwardTargetColumnId.HasValue
                    ? orderedColumns.FirstOrDefault(c => c.Id == column.BackwardTargetColumnId.Value)
                    : orderedColumns.Take(currentIdx).LastOrDefault(c => c.Type == ColumnType.Agent);

                if (backTarget is null)
                {
                    // First agent column — nowhere to go back, treat as forward
                    _logger.LogWarning("Column '{Col}' requested backward but has no target; treating as forward", column.Name);
                    goto case TransitionAction.Forward;
                }
                task.CurrentColumnId = backTarget.Id;
                task.Status = WorkTaskStatus.Waiting;
                task.PendingQuestion = null;
                break;

            case TransitionAction.Ask:
                task.Status = WorkTaskStatus.Asking;
                task.PendingQuestion = output.Question;
                break;
        }

        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (task.Status == WorkTaskStatus.Done && task.BranchName is not null)
            await TryPushAndMergeAsync(task, column, orderedColumns, ct);
    }

    private static string GenerateBranchName(string title)
    {
        var slug = System.Text.RegularExpressions.Regex.Replace(
                title.ToLowerInvariant(), @"[^a-z0-9]+", "-")
            .Trim('-');
        if (slug.Length > 50) slug = slug[..50].TrimEnd('-');
        return $"autocoder/{slug}";
    }

    private async Task<List<ShellCommandResult>> RunShellCommandsAsync(
        WorkTask task, Column column, IEnumerable<ColumnShellCommand> commands, CancellationToken ct)
    {
        var results = new List<ShellCommandResult>();
        var baseDir = task.WorktreePath
            ?? task.Board.Repositories.FirstOrDefault()?.LocalPath
            ?? Directory.GetCurrentDirectory();

        foreach (var cmd in commands)
        {
            var workDir = cmd.WorkingDirectory is not null
                ? Path.IsPathRooted(cmd.WorkingDirectory)
                    ? cmd.WorkingDirectory
                    : Path.Combine(baseDir, cmd.WorkingDirectory)
                : baseDir;

            if (!Directory.Exists(workDir))
            {
                _logger.LogWarning("Shell command working directory does not exist: {Dir}", workDir);
                results.Add(new ShellCommandResult(cmd.Command, -1, string.Empty, $"Working directory not found: {workDir}"));
                continue;
            }

            var (exe, args) = OperatingSystem.IsWindows()
                ? ("cmd.exe", $"/c {cmd.Command}")
                : ("sh", $"-c \"{cmd.Command.Replace("\"", "\\\"")}\"");

            var psi = new ProcessStartInfo(exe, args)
            {
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            try
            {
                using var proc = Process.Start(psi)
                    ?? throw new InvalidOperationException($"Failed to start process for: {cmd.Command}");

                var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
                var stderrTask = proc.StandardError.ReadToEndAsync(ct);
                await proc.WaitForExitAsync(ct);

                var stdout = await stdoutTask;
                var stderr = await stderrTask;
                results.Add(new ShellCommandResult(cmd.Command, proc.ExitCode, stdout, stderr));

                _logger.LogDebug("Shell [{Col}] $ {Cmd} → exit {Code}", column.Name, cmd.Command, proc.ExitCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Shell command failed to start: {Cmd}", cmd.Command);
                results.Add(new ShellCommandResult(cmd.Command, -1, string.Empty, ex.Message));
            }
        }

        return results;
    }

    private async Task<string> SummarizeShellResultsAsync(
        WorkTask task, Column column, List<ShellCommandResult> results, CancellationToken ct)
    {
        var prompt = _promptBuilder.BuildSummarizer(task, column, results);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            var result = await _agentRunner.RunAsync(prompt, cts.Token);
            return result.FullOutput.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Shell summarizer failed for task {TaskId}", task.Id);
            // Fall back to a mechanical summary
            var passed = results.Count(r => r.ExitCode == 0);
            var failed = results.Count(r => r.ExitCode != 0);
            return $"{passed} command(s) passed, {failed} failed.";
        }
    }

    private async Task TryPushAndMergeAsync(WorkTask task, Column sourceCol, List<Column> orderedColumns, CancellationToken ct)
    {
        bool success;
        try
        {
            success = await _gitService.PushAndMergeAsync(task, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PushAndMerge threw unexpectedly for task {TaskId}; leaving as Done", task.Id);
            return;
        }

        if (success) return;

        var sourceIdx = orderedColumns.FindIndex(c => c.Id == sourceCol.Id);
        var backTarget = sourceCol.BackwardTargetColumnId.HasValue
            ? orderedColumns.FirstOrDefault(c => c.Id == sourceCol.BackwardTargetColumnId.Value)
            : orderedColumns.Take(sourceIdx).LastOrDefault(c => c.Type == ColumnType.Agent);

        if (backTarget is null)
        {
            _logger.LogWarning("Merge conflict but no backward target from '{Col}'; leaving as Done", sourceCol.Name);
            return;
        }

        _db.ContextEntries.Add(new ContextEntry
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Kind = ContextEntryKind.SystemNote,
            Content = $"Merge conflict detected when merging branch '{task.BranchName}' into main. Please resolve the conflicts and push the branch again.",
            CreatedAt = DateTime.UtcNow
        });

        task.CurrentColumnId = backTarget.Id;
        task.Status = WorkTaskStatus.Waiting;
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ApproveTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await _db.WorkTasks
            .Include(t => t.Board).ThenInclude(b => b.Columns)
            .Include(t => t.Board).ThenInclude(b => b.Repositories)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new InvalidOperationException($"Task {taskId} not found.");

        if (task.Status != WorkTaskStatus.PendingApproval)
            throw new InvalidOperationException(
                $"Task is not pending approval (status: {task.Status}).");

        var orderedCols = task.Board.Columns.OrderBy(c => c.Position).ToList();
        var current = orderedCols.First(c => c.Id == task.CurrentColumnId);
        task.Status = current.Type == ColumnType.Input
            ? WorkTaskStatus.Done
            : WorkTaskStatus.Waiting;

        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (task.Status == WorkTaskStatus.Done && task.BranchName is not null)
        {
            var currentIdx = orderedCols.FindIndex(c => c.Id == task.CurrentColumnId);
            var lastAgentCol = orderedCols.Take(currentIdx).LastOrDefault(c => c.Type == ColumnType.Agent);
            if (lastAgentCol is not null)
                await TryPushAndMergeAsync(task, lastAgentCol, orderedCols, ct);
        }
    }

    public async Task SubmitAnswerAsync(Guid taskId, string answer, CancellationToken ct = default)
    {
        var task = await _db.WorkTasks
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new InvalidOperationException($"Task {taskId} not found.");

        if (task.Status != WorkTaskStatus.Asking)
            throw new InvalidOperationException(
                $"Task is not awaiting an answer (status: {task.Status}).");

        _db.ContextEntries.Add(new ContextEntry
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Kind = ContextEntryKind.UserAnswer,
            Content = answer,
            CreatedAt = DateTime.UtcNow
        });

        // Register before saving Waiting so BackgroundService skips this task
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _registry.Register(taskId, cts, tcs);
        try
        {
            task.Status = WorkTaskStatus.Waiting;
            task.PendingQuestion = null;
            task.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await ProcessTaskAsync(taskId, ct);
        }
        finally { _registry.Unregister(taskId); }
    }

    public async Task RetryTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await _db.WorkTasks
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new InvalidOperationException($"Task {taskId} not found.");

        if (task.Status != WorkTaskStatus.Error)
            throw new InvalidOperationException(
                $"Task is not in error state (status: {task.Status}). Only errored tasks can be retried.");

        _db.ContextEntries.Add(new ContextEntry
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Kind = ContextEntryKind.SystemNote,
            Content = "Previous run failed. Retrying. Complete the stage work and write a plain-text WORK SUMMARY at the end.",
            CreatedAt = DateTime.UtcNow
        });

        // Register before saving Waiting so BackgroundService skips this task
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _registry.Register(taskId, cts, tcs);
        try
        {
            task.Status = WorkTaskStatus.Waiting;
            task.ErrorMessage = null;
            task.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await ProcessTaskAsync(taskId, ct);
        }
        finally { _registry.Unregister(taskId); }
    }

    public async Task DeleteTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await _db.WorkTasks
            .Include(t => t.Board).ThenInclude(b => b.Repositories)
            .Include(t => t.ContextEntries)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new InvalidOperationException($"Task {taskId} not found.");

        if (task.Status == WorkTaskStatus.Running)
        {
            var finished = await _registry.CancelAndWaitAsync(taskId, TimeSpan.FromSeconds(10));
            if (!finished)
                _logger.LogWarning("Task {TaskId} did not stop within 10s during deletion; proceeding anyway.", taskId);
        }

        try
        {
            await _gitService.TeardownWorktreeAsync(task, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Worktree teardown failed for task {TaskId}; proceeding with deletion.", taskId);
        }

        _db.ContextEntries.RemoveRange(task.ContextEntries);
        _db.WorkTasks.Remove(task);
        await _db.SaveChangesAsync(ct);
    }

    private static bool IsRateLimited(string output) =>
        output.Contains("session limit", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("too many requests", StringComparison.OrdinalIgnoreCase);

    private async Task SetErrorAsync(WorkTask task, string message, CancellationToken ct)
    {
        task.Status = WorkTaskStatus.Error;
        task.ErrorMessage = message;
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
