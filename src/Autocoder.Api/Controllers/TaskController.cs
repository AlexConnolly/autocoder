using Autocoder.Api.Hubs;
using Autocoder.Core.Data;
using Autocoder.Core.Enums;
using Autocoder.Core.Interfaces;
using Autocoder.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Autocoder.Api.Controllers;

[ApiController]
public class TaskController : ControllerBase
{
    private readonly AutocoderDbContext db;
    private readonly IOrchestrator orchestrator;
    private readonly IHubContext<OrchestratorHub> hub;
    public TaskController(AutocoderDbContext db, IOrchestrator orchestrator, IHubContext<OrchestratorHub> hub)
    { this.db = db; this.orchestrator = orchestrator; this.hub = hub; }
    [HttpGet("api/boards/{boardId:guid}/tasks")]
    public async Task<ActionResult<List<WorkTask>>> ListTasks(Guid boardId, CancellationToken ct)
    {
        var tasks = await db.WorkTasks
            .Where(t => t.BoardId == boardId)
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync(ct);
        return Ok(tasks);
    }

    [HttpPost("api/boards/{boardId:guid}/tasks")]
    public async Task<ActionResult<WorkTask>> CreateTask(
        Guid boardId, CreateTaskRequest req, CancellationToken ct)
    {
        var board = await db.Boards
            .Include(b => b.Columns.OrderBy(c => c.Position))
            .FirstOrDefaultAsync(b => b.Id == boardId, ct);

        if (board is null) return NotFound("Board not found.");

        var firstAgentColumn = board.Columns.FirstOrDefault(c => c.Type == ColumnType.Agent);
        if (firstAgentColumn is null) return BadRequest("Board has no agent columns.");

        var task = new WorkTask
        {
            Id              = Guid.NewGuid(),
            BoardId         = boardId,
            Title           = req.Title,
            Description     = req.Description,
            CurrentColumnId = firstAgentColumn.Id,
            Status          = WorkTaskStatus.Waiting,
            CreatedAt       = DateTime.UtcNow,
            UpdatedAt       = DateTime.UtcNow,
        };

        db.WorkTasks.Add(task);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(ListTasks), new { boardId }, task);
    }

    [HttpGet("api/tasks/{id:guid}/context")]
    public async Task<ActionResult<List<ContextEntry>>> GetContext(Guid id, CancellationToken ct)
    {
        var entries = await db.ContextEntries
            .Where(e => e.TaskId == id)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);
        return Ok(entries);
    }

    [HttpPost("api/tasks/{id:guid}/answer")]
    public async Task<IActionResult> SubmitAnswer(
        Guid id, AnswerRequest req, CancellationToken ct)
    {
        try
        {
            await orchestrator.SubmitAnswerAsync(id, req.Text, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("api/tasks/{id:guid}/retry")]
    public async Task<IActionResult> RetryTask(Guid id, CancellationToken ct)
    {
        try
        {
            await orchestrator.RetryTaskAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("api/tasks/{id:guid}/approve")]
    public async Task<IActionResult> ApproveTask(Guid id, CancellationToken ct)
    {
        try
        {
            await orchestrator.ApproveTaskAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("api/tasks/{id:guid}")]
    public async Task<IActionResult> DeleteTask(Guid id, CancellationToken ct)
    {
        var task = await db.WorkTasks.FindAsync(new object[] { id }, ct);
        if (task is null) return NotFound();

        var boardId = task.BoardId;

        try
        {
            await orchestrator.DeleteTaskAsync(id, ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        await hub.Clients.Group($"board-{boardId}").SendAsync("TaskDeleted", id.ToString(), ct);
        return NoContent();
    }
}

public record CreateTaskRequest(string Title, string? Description);
public record AnswerRequest(string Text);
