using Autocoder.Core.Enums;

namespace Autocoder.Core.Models;

public class Column
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ColumnType Type { get; set; }
    public int Position { get; set; }
    public string? Instructions { get; set; }
    public string? OutputSchemaHint { get; set; }
    public Guid? BackwardTargetColumnId { get; set; }
    public bool AutoForward { get; set; }
    public bool AgentEnabled { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 300;
    public int MaxAgentTurns { get; set; } = 10;
    public Board Board { get; set; } = null!;
    public List<ColumnShellCommand> ShellCommands { get; set; } = new();
}
