using System.Diagnostics;
using Autocoder.Core.Interfaces;
using Autocoder.Core.Models;

namespace Autocoder.Api.Services;

public class GitService : IGitService
{
    private readonly string _worktreeBaseDir;
    private readonly ILogger<GitService> _logger;

    public GitService(IConfiguration config, ILogger<GitService> logger)
    {
        _worktreeBaseDir = config["Git:WorktreeBaseDir"]
            ?? Path.Combine(Path.GetTempPath(), "autocoder-worktrees");
        _logger = logger;
    }

    public async Task SetupWorktreeAsync(WorkTask task, CancellationToken ct = default)
    {
        var repos = task.Board?.Repositories;
        if (repos is null or { Count: 0 }) return;

        // Per-repo branch map from TaskRepository rows (enabled only)
        var repoBranchMap = task.TaskRepositories
            .Where(tr => tr.IsEnabled)
            .ToDictionary(tr => tr.RepositoryId, tr => tr.BranchName);
        var hasTaskRepos = task.TaskRepositories.Count > 0;

        string? firstWorktreePath = null;

        foreach (var repo in repos)
        {
            string? branchName;
            if (hasTaskRepos)
            {
                if (!repoBranchMap.TryGetValue(repo.Id, out branchName))
                    continue; // repo is disabled
            }
            else
            {
                // Legacy: single branch name for all repos
                branchName = task.BranchName;
            }

            if (string.IsNullOrWhiteSpace(branchName))
            {
                _logger.LogWarning("SetupWorktreeAsync: no branch name for repo {Repo} on task {Id}", repo.Name, task.Id);
                continue;
            }

            if (!Directory.Exists(repo.LocalPath))
            {
                _logger.LogWarning("Repository path does not exist: {Path}", repo.LocalPath);
                continue;
            }

            var worktreeDir = GetWorktreePath(task.Id, repo.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(worktreeDir)!);

            // Create branch from default branch (ignore error if branch already exists)
            await RunGitAsync(repo.LocalPath,
                $"branch {branchName} origin/{repo.DefaultBranch}",
                ct, throwOnError: false);

            // Fallback: branch from local default branch
            await RunGitAsync(repo.LocalPath,
                $"branch {branchName} {repo.DefaultBranch}",
                ct, throwOnError: false);

            if (!Directory.Exists(worktreeDir))
            {
                // throwOnError: false — if the branch is already checked out elsewhere the task
                // will run in the main repo dir rather than crashing entirely
                await RunGitAsync(repo.LocalPath,
                    $"worktree add \"{worktreeDir}\" {branchName}",
                    ct, throwOnError: false);

                if (!Directory.Exists(worktreeDir))
                {
                    _logger.LogWarning(
                        "Worktree dir not created for task {Id} branch {Branch} — agent will run in repo root",
                        task.Id, branchName);
                    continue;
                }
            }

            firstWorktreePath ??= worktreeDir;
        }

        task.WorktreePath = firstWorktreePath;
    }

    public async Task<bool> PushAndMergeAsync(WorkTask task, CancellationToken ct = default)
    {
        var repos = task.Board?.Repositories;
        if (repos is null or { Count: 0 }) return true;

        var repoBranchMap = task.TaskRepositories
            .Where(tr => tr.IsEnabled)
            .ToDictionary(tr => tr.RepositoryId, tr => tr.BranchName);
        var hasTaskRepos = task.TaskRepositories.Count > 0;

        var anyConflict = false;

        foreach (var repo in repos)
        {
            string? branchName;
            if (hasTaskRepos)
            {
                if (!repoBranchMap.TryGetValue(repo.Id, out branchName))
                    continue; // repo is disabled
            }
            else
            {
                branchName = task.BranchName;
            }

            if (string.IsNullOrWhiteSpace(branchName)) continue;

            if (!Directory.Exists(repo.LocalPath))
            {
                _logger.LogWarning("Repository path does not exist: {Path}", repo.LocalPath);
                continue;
            }

            // Push feature branch to origin (retry 3x)
            await RunGitWithRetryAsync(repo.LocalPath, $"push origin {branchName}", ct);

            // Stash tracked changes only — --include-untracked can fail on locked files (e.g. api.log)
            await RunGitAsync(repo.LocalPath, "stash -m \"autocoder-pre-merge\"", ct, throwOnError: false);

            await RunGitAsync(repo.LocalPath,
                $"checkout {repo.DefaultBranch}",
                ct, throwOnError: false);

            var exitCode = await RunGitAsync(repo.LocalPath,
                $"merge --no-ff {branchName} -m \"Merge branch '{branchName}'\"",
                ct, throwOnError: false, returnExitCode: true);

            if (exitCode != 0)
            {
                _logger.LogWarning(
                    "Merge conflict for {Branch} into {Default} in repo {Repo} — aborting",
                    branchName, repo.DefaultBranch, repo.Name);
                await RunGitAsync(repo.LocalPath, "merge --abort", ct, throwOnError: false);
                await RunGitAsync(repo.LocalPath, "stash pop", ct, throwOnError: false);
                anyConflict = true;
                continue;
            }

            // Restore any stashed changes after merge
            await RunGitAsync(repo.LocalPath, "stash pop", ct, throwOnError: false);

            // Push default branch to origin (retry 3x)
            await RunGitWithRetryAsync(repo.LocalPath, $"push origin {repo.DefaultBranch}", ct);
        }

        return !anyConflict;
    }

    public async Task TeardownWorktreeAsync(WorkTask task, CancellationToken ct = default)
    {
        var repos = task.Board?.Repositories;
        if (repos is null or { Count: 0 }) return;

        foreach (var repo in repos)
        {
            if (!Directory.Exists(repo.LocalPath)) continue;
            var worktreeDir = GetWorktreePath(task.Id, repo.Name);
            if (!Directory.Exists(worktreeDir)) continue;

            await RunGitAsync(repo.LocalPath,
                $"worktree remove \"{worktreeDir}\" --force",
                ct, throwOnError: false);
        }
    }

    public async Task<List<string>> ListBranchesAsync(BoardRepository repo, CancellationToken ct = default)
    {
        if (!Directory.Exists(repo.LocalPath)) return new List<string>();

        var output = await RunGitOutputAsync(repo.LocalPath, "branch --list", ct);
        var branches = new List<string>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = line.Trim().TrimStart('*').Trim();
            if (!string.IsNullOrEmpty(name))
                branches.Add(name);
        }
        return branches.OrderBy(b => b).ToList();
    }

    public async Task<List<string>> ListAutocoderBranchesAsync(IEnumerable<BoardRepository> repos, CancellationToken ct = default)
    {
        var branches = new HashSet<string>(StringComparer.Ordinal);

        foreach (var repo in repos)
        {
            if (!Directory.Exists(repo.LocalPath)) continue;

            var output = await RunGitOutputAsync(repo.LocalPath, "branch --list \"auto/*\"", ct);
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var name = line.Trim().TrimStart('*').Trim();
                if (!string.IsNullOrEmpty(name))
                    branches.Add(name);
            }
        }

        return branches.OrderBy(b => b).ToList();
    }

    public async Task DeleteBranchAsync(string branchName, IEnumerable<BoardRepository> repos, CancellationToken ct = default)
    {
        foreach (var repo in repos)
        {
            if (!Directory.Exists(repo.LocalPath)) continue;

            await RunGitAsync(repo.LocalPath, $"branch -D \"{branchName}\"", ct, throwOnError: false);
            await RunGitAsync(repo.LocalPath, $"push origin --delete \"{branchName}\"", ct, throwOnError: false);
        }
    }

    private string GetWorktreePath(Guid taskId, string repoName) =>
        Path.Combine(_worktreeBaseDir, taskId.ToString(), repoName);

    private async Task RunGitWithRetryAsync(string repoPath, string args, CancellationToken ct,
        int maxAttempts = 3, int delayMs = 2000)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var code = await RunGitAsync(repoPath, args, ct, throwOnError: false, returnExitCode: true);
            if (code == 0 || attempt == maxAttempts)
            {
                if (code != 0)
                    _logger.LogWarning("git {Args} failed after {Max} attempts", args, maxAttempts);
                return;
            }
            _logger.LogWarning("git {Args} failed (attempt {A}/{Max}), retrying in {D}ms", args, attempt, maxAttempts, delayMs);
            await Task.Delay(delayMs, ct);
        }
    }

    private async Task<string> RunGitOutputAsync(string repoPath, string args, CancellationToken ct)
    {
        _logger.LogDebug("git -C \"{Repo}\" {Args} [capturing output]", repoPath, args);

        var psi = new ProcessStartInfo("git", args)
        {
            WorkingDirectory       = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var output = await p.StandardOutput.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        return output;
    }

    private async Task RunGitAsync(string repoPath, string args, CancellationToken ct, bool throwOnError = true) =>
        await RunGitAsync(repoPath, args, ct, throwOnError, returnExitCode: false);

    private async Task<int> RunGitAsync(string repoPath, string args, CancellationToken ct, bool throwOnError, bool returnExitCode)
    {
        _logger.LogDebug("git -C \"{Repo}\" {Args}", repoPath, args);

        var psi = new ProcessStartInfo("git", args)
        {
            WorkingDirectory       = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        await p.WaitForExitAsync(ct);

        if (throwOnError && p.ExitCode != 0)
        {
            var err = await p.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException(
                $"git {args} failed (exit {p.ExitCode}): {err.Trim()}");
        }

        return p.ExitCode;
    }
}
