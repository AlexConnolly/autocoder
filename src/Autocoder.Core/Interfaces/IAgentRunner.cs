using Autocoder.Core.Orchestration;

namespace Autocoder.Core.Interfaces;

public interface IAgentRunner
{
    Task<AgentResult> RunAsync(AgentPrompt prompt, CancellationToken ct = default);
}
