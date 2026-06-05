namespace Autocoder.Core.Models;

public enum ShellCommandPhase { Pre = 0, Post = 1 }

public class ColumnShellCommand
{
    public Guid Id { get; set; }
    public Guid ColumnId { get; set; }
    public string Command { get; set; } = string.Empty;
    public string? WorkingDirectory { get; set; }
    public int Position { get; set; }
    public ShellCommandPhase Phase { get; set; } = ShellCommandPhase.Post;
    public Column Column { get; set; } = null!;
}
