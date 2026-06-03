using Autocoder.Core.Enums;

namespace Autocoder.Core.Models;

public class ContextEntry
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public ContextEntryKind Kind { get; set; }
    public Guid? ColumnId { get; set; }
    public string? ColumnName { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? StructuredData { get; set; }
    public TransitionAction? Action { get; set; }
    public DateTime CreatedAt { get; set; }
    public WorkTask Task { get; set; } = null!;
}
