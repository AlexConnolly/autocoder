using Autocoder.Core.Models;

namespace Autocoder.Core.Interfaces;

public interface IGitService
{
    Task SetupWorktreeAsync(WorkTask task, CancellationToken ct = default);
    Task TeardownWorktreeAsync(WorkTask task, CancellationToken ct = default);
    Task<bool> PushAndMergeAsync(WorkTask task, CancellationToken ct = default);
    Task<List<string>> ListAutocoderBranchesAsync(IEnumerable<BoardRepository> repos, CancellationToken ct = default);
    Task DeleteBranchAsync(string branchName, IEnumerable<BoardRepository> repos, CancellationToken ct = default);
}
