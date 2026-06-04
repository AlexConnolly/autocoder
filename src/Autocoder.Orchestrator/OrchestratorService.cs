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

        task.Status = WorkTaskStatus.Running;
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var orderedColumns = task.Board.Columns.OrderBy(c => c.Position).ToList();

        // ── Phase 1: Worker ──────────────────────────────────────────────────
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

        var workerOutput = workerResult.FullOutput;

        // ── Phase 2: Determiner ──────────────────────────────────────────────
        var determinerPrompt = _promptBuilder.BuildDeterminer(task, column, workerOutput);

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

        if (determinerResult.StructuredOutput is null)
        {
            var snippet = determinerResult.FullOutput.Length > 3000
                ? determinerResult.FullOutput[..3000] + "\n...[truncated]"
                : determinerResult.FullOutput;
            _logger.LogError(
                "Determiner parse failed for task {TaskId}. Output ({Len} chars):\n{Output}",
                taskId, determinerResult.FullOutput.Length, snippet);
            await SetErrorAsync(task, "Determiner did not produce a valid routing decision.", ct);
            return;
        }

        var output = determinerResult.StructuredOutput;

        var defaultBranches = task.Board.Repositories.Select(r => r.DefaultBranch).ToHashSet(StringComparer.OrdinalIgnoreCase);
        defaultBranches.Add("main");
        defaultBranches.Add("master");

        if (task.BranchName is null
            && output.TryGetString("branchName", out var branchName)
            && !string.IsNullOrWhiteSpace(branchName)
            && !defaultBranches.Contains(branchName))
        {
            task.BranchName = branchName;
            await _gitService.SetupWorktreeAsync(task, ct);
        }

        _db.ContextEntries.Add(new ContextEntry
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Kind = ContextEntryKind.AgentOutput,
            ColumnId = column.Id,
            ColumnName = column.Name,
            Content = workerOutput,
            StructuredData = output.RawJson,
            Action = output.Action,
            CreatedAt = DateTime.UtcNow
        });

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
                    if (next.Type == ColumnType.Input)
                        task.Status = WorkTaskStatus.Done;
                    else if (column.AutoForward)
                        task.Status = WorkTaskStatus.Waiting;
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
    }

    public async Task ApproveTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await _db.WorkTasks
            .Include(t => t.Board).ThenInclude(b => b.Columns)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new InvalidOperationException($"Task {taskId} not found.");

        if (task.Status != WorkTaskStatus.PendingApproval)
            throw new InvalidOperationException(
                $"Task is not pending approval (status: {task.Status}).");

        var current = task.Board.Columns.First(c => c.Id == task.CurrentColumnId);
        task.Status = current.Type == ColumnType.Input
            ? WorkTaskStatus.Done
            : WorkTaskStatus.Waiting;

        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
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

        task.Status = WorkTaskStatus.Waiting;
        task.PendingQuestion = null;
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await ProcessTaskAsync(taskId, ct);
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

        task.Status = WorkTaskStatus.Waiting;
        task.ErrorMessage = null;
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await ProcessTaskAsync(taskId, ct);
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

    private async Task SetErrorAsync(WorkTask task, string message, CancellationToken ct)
    {
        task.Status = WorkTaskStatus.Error;
        task.ErrorMessage = message;
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
