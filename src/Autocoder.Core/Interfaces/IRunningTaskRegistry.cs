namespace Autocoder.Core.Interfaces;

public interface IRunningTaskRegistry
{
    void Register(Guid taskId, CancellationTokenSource cts, TaskCompletionSource<bool> completion);
    void Unregister(Guid taskId);
    bool IsRegistered(Guid taskId);
    Task<bool> CancelAndWaitAsync(Guid taskId, TimeSpan timeout);
}
