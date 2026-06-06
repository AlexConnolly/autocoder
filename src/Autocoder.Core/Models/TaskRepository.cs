namespace Autocoder.Core.Models;

public class TaskRepository
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid RepositoryId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public WorkTask Task { get; set; } = null!;
    public BoardRepository Repository { get; set; } = null!;
}
