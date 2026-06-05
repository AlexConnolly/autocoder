using Autocoder.Core.Enums;

namespace Autocoder.Core.Models;

public class WorkTask
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CurrentColumnId { get; set; }
    public WorkTaskStatus Status { get; set; }
    public string? BranchName { get; set; }
    public string? WorktreePath { get; set; }
    public string? PendingQuestion { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Board Board { get; set; } = null!;
    public List<ContextEntry> ContextEntries { get; set; } = new();
    public List<TaskRepository> TaskRepositories { get; set; } = new();
}
