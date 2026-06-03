using Autocoder.Core.Data;
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
        await db.SaveChangesAsync(ct);
        return Ok(board);
    }

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

    [HttpGet("{boardId:guid}/columns")]
    public async Task<ActionResult<List<Column>>> GetColumns(Guid boardId, CancellationToken ct)
    {
        var cols = await db.Columns
            .Where(c => c.BoardId == boardId)
            .OrderBy(c => c.Position)
            .ToListAsync(ct);
        return Ok(cols);
    }

    [HttpPut("/api/columns/{id:guid}")]
    public async Task<ActionResult<Column>> UpdateColumn(
        Guid id, UpdateColumnRequest req, CancellationToken ct)
    {
        var col = await db.Columns.FindAsync(new object[] { id }, ct);
        if (col is null) return NotFound();

        col.Instructions           = req.Instructions;
        col.OutputSchemaHint       = req.OutputSchemaHint;
        col.AutoForward            = req.AutoForward;
        col.BackwardTargetColumnId = req.BackwardTargetColumnId;
        col.TimeoutSeconds         = req.TimeoutSeconds;
        col.MaxAgentTurns          = req.MaxAgentTurns;
        await db.SaveChangesAsync(ct);
        return Ok(col);
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
}

public record UpdateBoardRequest(string Name, string? GlobalInstructions);
public record AddRepositoryRequest(string Name, string LocalPath, string? DefaultBranch);
public record UpdateColumnRequest(
    string? Instructions, string? OutputSchemaHint, bool AutoForward, Guid? BackwardTargetColumnId,
    int TimeoutSeconds, int MaxAgentTurns);
public record ReorderRequest(List<Guid> Ids);
