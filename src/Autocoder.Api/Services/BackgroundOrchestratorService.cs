using Autocoder.Api.Hubs;
using Autocoder.Core.Data;
using Autocoder.Core.Enums;
using Autocoder.Core.Interfaces;
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

        // Find waiting tasks in agent columns
        var waitingTasks = await db.WorkTasks
            .Include(t => t.Board).ThenInclude(b => b.Columns)
            .Where(t => t.Status == WorkTaskStatus.Waiting)
            .ToListAsync(ct);

        var eligible = waitingTasks.Where(t =>
        {
            var col = t.Board.Columns.FirstOrDefault(c => c.Id == t.CurrentColumnId);
            return col?.Type == ColumnType.Agent;
        }).ToList();

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
