namespace Autocoder.Core.Orchestration;

public enum AgentPromptKind { Worker, Determiner, Summarizer }

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
    public AgentPromptKind Kind { get; set; } = AgentPromptKind.Worker;
    public bool StreamJson => Kind == AgentPromptKind.Worker;
    public bool CavemanMode { get; set; }
}
