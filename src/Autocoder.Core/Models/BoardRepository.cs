namespace Autocoder.Core.Models;

public class BoardRepository
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = "main";
    public Board Board { get; set; } = null!;
}
