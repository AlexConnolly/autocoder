namespace Autocoder.Core.Interfaces;

public interface IOrchestrator
{
    Task ProcessTaskAsync(Guid taskId, CancellationToken ct = default);
    Task ApproveTaskAsync(Guid taskId, CancellationToken ct = default);
    Task SubmitAnswerAsync(Guid taskId, string answer, CancellationToken ct = default);
    Task RetryTaskAsync(Guid taskId, CancellationToken ct = default);
    Task DeleteTaskAsync(Guid taskId, CancellationToken ct = default);
}
