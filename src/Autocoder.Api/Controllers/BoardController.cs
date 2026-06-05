using Autocoder.Core.Data;
using Autocoder.Core.Enums;
using Autocoder.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Autocoder.Api.Controllers;

[ApiController]
[Route("api/boards")]
public class BoardController : ControllerBase
{
    private readonly AutocoderDbContext db;
    public BoardController(AutocoderDbContext db) { this.db = db; }

    [HttpGet("{boardId:guid}")]
    public async Task<ActionResult<Board>> GetBoard(Guid boardId, CancellationToken ct)
    {
        var board = await db.Boards
            .Include(b => b.Columns.OrderBy(c => c.Position))
                .ThenInclude(c => c.ShellCommands)
            .Include(b => b.Repositories)
            .FirstOrDefaultAsync(b => b.Id == boardId, ct);

        return board is null ? NotFound() : Ok(board);
    }

    [HttpPut("{boardId:guid}")]
    public async Task<ActionResult<Board>> UpdateBoard(
        Guid boardId, UpdateBoardRequest req, CancellationToken ct)
    {
        var board = await db.Boards.FindAsync(new object[] { boardId }, ct);
        if (board is null) return NotFound();

        board.Name = req.Name;
        board.GlobalInstructions = req.GlobalInstructions;
        board.MaxInProgress = req.MaxInProgress;
        board.CavemanMode = req.CavemanMode;
        await db.SaveChangesAsync(ct);
        return Ok(board);
    }

    // ── Columns ───────────────────────────────────────────────────────────────

    [HttpGet("{boardId:guid}/columns")]
    public async Task<ActionResult<List<Column>>> GetColumns(Guid boardId, CancellationToken ct)
    {
        var cols = await db.Columns
            .Include(c => c.ShellCommands)
            .Where(c => c.BoardId == boardId)
            .OrderBy(c => c.Position)
            .ToListAsync(ct);
        return Ok(cols);
    }

    [HttpPost("{boardId:guid}/columns")]
    public async Task<ActionResult<Column>> CreateColumn(
        Guid boardId, CreateColumnRequest req, CancellationToken ct)
    {
        var maxPos = await db.Columns
            .Where(c => c.BoardId == boardId)
            .MaxAsync(c => (int?)c.Position, ct) ?? -1;

        var col = new Column
        {
            Id            = Guid.NewGuid(),
            BoardId       = boardId,
            Name          = req.Name,
            Type          = req.Type,
            Position      = maxPos + 1,
            AgentEnabled  = req.Type == ColumnType.Agent,
            AutoForward   = false,
            TimeoutSeconds = 300,
            MaxAgentTurns = 10,
        };
        db.Columns.Add(col);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetColumns), new { boardId }, col);
    }

    [HttpPut("/api/columns/{id:guid}")]
    public async Task<ActionResult<Column>> UpdateColumn(
        Guid id, UpdateColumnRequest req, CancellationToken ct)
    {
        var col = await db.Columns.FindAsync(new object[] { id }, ct);
        if (col is null) return NotFound();

        col.Name                   = req.Name;
        col.Instructions           = req.Instructions;
        col.OutputSchemaHint       = req.OutputSchemaHint;
        col.AutoForward            = req.AutoForward;
        col.AgentEnabled           = req.AgentEnabled;
        col.BackwardTargetColumnId = req.BackwardTargetColumnId;
        col.TimeoutSeconds         = req.TimeoutSeconds;
        col.MaxAgentTurns          = req.MaxAgentTurns;
        await db.SaveChangesAsync(ct);
        return Ok(col);
    }

    [HttpDelete("/api/columns/{id:guid}")]
    public async Task<IActionResult> DeleteColumn(Guid id, CancellationToken ct)
    {
        var col = await db.Columns
            .Include(c => c.ShellCommands)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (col is null) return NotFound();

        db.ColumnShellCommands.RemoveRange(col.ShellCommands);
        db.Columns.Remove(col);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("{boardId:guid}/columns/reorder")]
    public async Task<IActionResult> ReorderColumns(
        Guid boardId, ReorderRequest req, CancellationToken ct)
    {
        var cols = await db.Columns.Where(c => c.BoardId == boardId).ToListAsync(ct);
        for (var i = 0; i < req.Ids.Count; i++)
        {
            var col = cols.FirstOrDefault(c => c.Id == req.Ids[i]);
            if (col is not null) col.Position = i;
        }
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Shell commands ────────────────────────────────────────────────────────

    [HttpGet("/api/columns/{columnId:guid}/shell-commands")]
    public async Task<ActionResult<List<ColumnShellCommand>>> GetShellCommands(
        Guid columnId, CancellationToken ct)
    {
        var cmds = await db.ColumnShellCommands
            .Where(s => s.ColumnId == columnId)
            .OrderBy(s => s.Position)
            .ToListAsync(ct);
        return Ok(cmds);
    }

    [HttpPost("/api/columns/{columnId:guid}/shell-commands")]
    public async Task<ActionResult<ColumnShellCommand>> AddShellCommand(
        Guid columnId, ShellCommandRequest req, CancellationToken ct)
    {
        var maxPos = await db.ColumnShellCommands
            .Where(s => s.ColumnId == columnId)
            .MaxAsync(s => (int?)s.Position, ct) ?? -1;

        var cmd = new ColumnShellCommand
        {
            Id               = Guid.NewGuid(),
            ColumnId         = columnId,
            Command          = req.Command,
            WorkingDirectory = string.IsNullOrWhiteSpace(req.WorkingDirectory) ? null : req.WorkingDirectory,
            Position         = maxPos + 1,
        };
        db.ColumnShellCommands.Add(cmd);
        await db.SaveChangesAsync(ct);
        return Created(string.Empty, cmd);
    }

    [HttpPut("/api/shell-commands/{id:guid}")]
    public async Task<ActionResult<ColumnShellCommand>> UpdateShellCommand(
        Guid id, ShellCommandRequest req, CancellationToken ct)
    {
        var cmd = await db.ColumnShellCommands.FindAsync(new object[] { id }, ct);
        if (cmd is null) return NotFound();

        cmd.Command          = req.Command;
        cmd.WorkingDirectory = string.IsNullOrWhiteSpace(req.WorkingDirectory) ? null : req.WorkingDirectory;
        await db.SaveChangesAsync(ct);
        return Ok(cmd);
    }

    [HttpDelete("/api/shell-commands/{id:guid}")]
    public async Task<IActionResult> DeleteShellCommand(Guid id, CancellationToken ct)
    {
        var cmd = await db.ColumnShellCommands.FindAsync(new object[] { id }, ct);
        if (cmd is null) return NotFound();

        db.ColumnShellCommands.Remove(cmd);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Repositories ──────────────────────────────────────────────────────────

    [HttpGet("{boardId:guid}/repositories")]
    public async Task<ActionResult<List<BoardRepository>>> GetRepositories(Guid boardId, CancellationToken ct)
    {
        var repos = await db.Repositories
            .Where(r => r.BoardId == boardId)
            .ToListAsync(ct);
        return Ok(repos);
    }

    [HttpPost("{boardId:guid}/repositories")]
    public async Task<ActionResult<BoardRepository>> AddRepository(
        Guid boardId, AddRepositoryRequest req, CancellationToken ct)
    {
        var repo = new BoardRepository
        {
            Id            = Guid.NewGuid(),
            BoardId       = boardId,
            Name          = req.Name,
            LocalPath     = req.LocalPath,
            DefaultBranch = req.DefaultBranch ?? "main",
        };
        db.Repositories.Add(repo);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetRepositories), new { boardId }, repo);
    }

    [HttpPut("/api/repositories/{id:guid}")]
    public async Task<ActionResult<BoardRepository>> UpdateRepository(
        Guid id, AddRepositoryRequest req, CancellationToken ct)
    {
        var repo = await db.Repositories.FindAsync(new object[] { id }, ct);
        if (repo is null) return NotFound();

        repo.Name          = req.Name;
        repo.LocalPath     = req.LocalPath;
        repo.DefaultBranch = req.DefaultBranch ?? "main";
        await db.SaveChangesAsync(ct);
        return Ok(repo);
    }

    [HttpDelete("/api/repositories/{id:guid}")]
    public async Task<IActionResult> DeleteRepository(Guid id, CancellationToken ct)
    {
        var repo = await db.Repositories.FindAsync(new object[] { id }, ct);
        if (repo is null) return NotFound();

        db.Repositories.Remove(repo);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public record UpdateBoardRequest(string Name, string? GlobalInstructions, int? MaxInProgress, bool CavemanMode);
public record CreateColumnRequest(string Name, ColumnType Type);
public record UpdateColumnRequest(
    string Name, string? Instructions, string? OutputSchemaHint,
    bool AutoForward, bool AgentEnabled,
    Guid? BackwardTargetColumnId, int TimeoutSeconds, int MaxAgentTurns);
public record ShellCommandRequest(string Command, string? WorkingDirectory);
public record AddRepositoryRequest(string Name, string LocalPath, string? DefaultBranch);
public record ReorderRequest(List<Guid> Ids);
