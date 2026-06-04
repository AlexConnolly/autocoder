namespace Autocoder.Core.Models;

public class Board
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? GlobalInstructions { get; set; }
    public int? MaxInProgress { get; set; }
    public List<Column> Columns { get; set; } = new();
    public List<BoardRepository> Repositories { get; set; } = new();
    public List<WorkTask> Tasks { get; set; } = new();
}
