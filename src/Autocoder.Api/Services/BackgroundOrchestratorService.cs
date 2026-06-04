using Autocoder.Api.Hubs;
using Autocoder.Core.Data;
using Autocoder.Core.Enums;
using Autocoder.Core.Interfaces;
using Autocoder.Core.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace Autocoder.Api.Services;

public class BackgroundOrchestratorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<OrchestratorHub> _hub;
    private readonly ILogger<BackgroundOrchestratorService> _logger;
    private readonly SemaphoreSlim _semaphore;
    private readonly int _pollIntervalMs;
    private readonly IRunningTaskRegistry _registry;

    public BackgroundOrchestratorService(
        IServiceScopeFactory scopeFactory,
        IHubContext<OrchestratorHub> hub,
        IConfiguration config,
        ILogger<BackgroundOrchestratorService> logger,
        IRunningTaskRegistry registry)
    {
        _scopeFactory   = scopeFactory;
        _hub            = hub;
        _logger         = logger;
        _pollIntervalMs = config.GetValue("Orchestrator:PollIntervalMs", 1000);
        _semaphore      = new SemaphoreSlim(config.GetValue("Orchestrator:MaxConcurrency", 3));
        _registry       = registry;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Orchestrator background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PickUpWaitingTasksAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unexpected error in orchestrator poll loop.");
            }

            await Task.Delay(_pollIntervalMs, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task PickUpWaitingTasksAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutocoderDbContext>();

        // Load all non-terminal tasks so we can count in-progress for WIP limit
        var activeTasks = await db.WorkTasks
            .Include(t => t.Board).ThenInclude(b => b.Columns)
            .Where(t => t.Status != WorkTaskStatus.Done && t.Status != WorkTaskStatus.Error)
            .ToListAsync(ct);

        var waiting = activeTasks
            .Where(t => t.Status == WorkTaskStatus.Waiting)
            .Where(t =>
            {
                var col = t.Board.Columns.FirstOrDefault(c => c.Id == t.CurrentColumnId);
                return col?.Type == ColumnType.Agent && !_registry.IsRegistered(t.Id);
            })
            .ToList();

        // Track tasks queued for dispatch this cycle per board so the WIP check
        // stays accurate as we iterate — prevents multiple tasks seeing the same
        // stale in-progress count when the limit would only allow one more.
        var pendingDispatchPerBoard = new Dictionary<Guid, int>();

        var eligible = new List<WorkTask>();
        foreach (var task in waiting)
        {
            if (task.Board.MaxInProgress.HasValue)
            {
                var orderedCols = task.Board.Columns.OrderBy(c => c.Position).ToList();
                var firstAgentCol = orderedCols.FirstOrDefault(c => c.Type == ColumnType.Agent);

                // Only enforce the limit for tasks entering from the first agent column
                if (task.CurrentColumnId == firstAgentCol?.Id)
                {
                    var firstColId = orderedCols.First().Id;
                    var lastColId = orderedCols.Last().Id;
                    var firstAgentColId = firstAgentCol.Id;

                    // Count tasks actively in-progress (not in first/last col, not queued in entry column)
                    var inProgressCount = activeTasks.Count(bt =>
                        bt.BoardId == task.BoardId &&
                        bt.CurrentColumnId != firstColId &&
                        bt.CurrentColumnId != lastColId &&
                        !(bt.Status == WorkTaskStatus.Waiting && bt.CurrentColumnId == firstAgentColId));

                    var pendingCount = pendingDispatchPerBoard.GetValueOrDefault(task.BoardId, 0);

                    if (inProgressCount + pendingCount >= task.Board.MaxInProgress.Value)
                        continue;
                }
            }

            pendingDispatchPerBoard[task.BoardId] = pendingDispatchPerBoard.GetValueOrDefault(task.BoardId, 0) + 1;
            eligible.Add(task);
        }

        foreach (var task in eligible)
        {
            // Try to acquire a concurrency slot (non-blocking)
            if (!await _semaphore.WaitAsync(0, ct)) break;

            var taskId  = task.Id;
            var boardId = task.BoardId;

            var perTaskCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _registry.Register(taskId, perTaskCts, tcs);

            _ = Task.Run(async () =>
            {
                var runStartedAt = DateTime.UtcNow;
                try
                {
                    using var taskScope = _scopeFactory.CreateScope();
                    var orchestrator = taskScope.ServiceProvider.GetRequiredService<IOrchestrator>();
                    await orchestrator.ProcessTaskAsync(taskId, perTaskCts.Token);

                    await BroadcastNewContextEntriesAsync(taskId, boardId, runStartedAt, ct);
                    await BroadcastTaskUpdatedAsync(taskId, boardId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing task {TaskId}", taskId);
                    // Ensure the task never stays stuck in Running
                    try
                    {
                        using var errorScope = _scopeFactory.CreateScope();
                        var errorDb = errorScope.ServiceProvider.GetRequiredService<AutocoderDbContext>();
                        var stuck = await errorDb.WorkTasks.FindAsync(new object[] { taskId }, ct);
                        if (stuck is not null && stuck.Status == WorkTaskStatus.Running)
                        {
                            stuck.Status       = WorkTaskStatus.Error;
                            stuck.ErrorMessage = $"Unexpected error: {ex.Message}";
                            stuck.UpdatedAt    = DateTime.UtcNow;
                            await errorDb.SaveChangesAsync(ct);
                        }
                    }
                    catch (Exception inner)
                    {
                        _logger.LogError(inner, "Failed to mark task {TaskId} as errored after crash", taskId);
                    }
                    await BroadcastTaskUpdatedAsync(taskId, boardId, ct);
                }
                finally
                {
                    _registry.Unregister(taskId);
                    _semaphore.Release();
                }
            }, ct);
        }
    }

    private async Task BroadcastNewContextEntriesAsync(Guid taskId, Guid boardId, DateTime since, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AutocoderDbContext>();
            var entries = await db.ContextEntries
                .Where(e => e.TaskId == taskId && e.CreatedAt >= since)
                .OrderBy(e => e.CreatedAt)
                .ToListAsync(ct);

            var group = $"board-{boardId}";
            foreach (var entry in entries)
                await _hub.Clients.Group(group).SendAsync("ContextEntryAdded", taskId.ToString(), entry, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast ContextEntryAdded for task {TaskId}", taskId);
        }
    }

    private async Task BroadcastTaskUpdatedAsync(Guid taskId, Guid boardId, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AutocoderDbContext>();
            var task = await db.WorkTasks.FindAsync(new object[] { taskId }, ct);
            if (task is null) return;

            await _hub.Clients
                .Group($"board-{boardId}")
                .SendAsync("TaskUpdated", task, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast TaskUpdated for {TaskId}", taskId);
        }
    }
}
