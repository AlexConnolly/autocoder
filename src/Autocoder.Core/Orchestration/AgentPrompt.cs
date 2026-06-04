namespace Autocoder.Core.Orchestration;

public class AgentPrompt
{
    public Guid ColumnId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int MaxTurns { get; set; } = 10;

    // Runtime fields — not part of prompt content, used by the runner
    public Guid TaskId { get; set; }
    public Guid BoardId { get; set; }
    public string? WorktreePath { get; set; }
    public bool StreamJson { get; set; } = true;
}
