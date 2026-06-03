using Autocoder.Core.Interfaces;
using System.Collections.Concurrent;

namespace Autocoder.Api.Services;

public class RunningTaskRegistry : IRunningTaskRegistry
{
    private readonly ConcurrentDictionary<Guid, (CancellationTokenSource Cts, TaskCompletionSource<bool> Tcs)> _tasks = new();

    public void Register(Guid taskId, CancellationTokenSource cts, TaskCompletionSource<bool> completion)
    {
        _tasks[taskId] = (cts, completion);
    }

    public void Unregister(Guid taskId)
    {
        if (_tasks.TryRemove(taskId, out var entry))
        {
            entry.Tcs.TrySetResult(true);
            entry.Cts.Dispose();
        }
    }

    public async Task<bool> CancelAndWaitAsync(Guid taskId, TimeSpan timeout)
    {
        if (!_tasks.TryGetValue(taskId, out var entry))
            return true;

        entry.Cts.Cancel();
        var completed = await Task.WhenAny(entry.Tcs.Task, Task.Delay(timeout));
        return completed == entry.Tcs.Task;
    }
}
