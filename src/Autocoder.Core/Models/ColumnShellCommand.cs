namespace Autocoder.Core.Models;

public class ColumnShellCommand
{
    public Guid Id { get; set; }
    public Guid ColumnId { get; set; }
    public string Command { get; set; } = string.Empty;
    public string? WorkingDirectory { get; set; }
    public int Position { get; set; }
    public Column Column { get; set; } = null!;
}
