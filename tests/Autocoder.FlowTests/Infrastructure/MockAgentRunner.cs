using Autocoder.Core.Interfaces;
using Autocoder.Core.Orchestration;

namespace Autocoder.FlowTests.Infrastructure;

public class MockAgentRunner : IAgentRunner
{
    private readonly Dictionary<string, Queue<AgentResult>> _columnQueues = new();
    private readonly Dictionary<string, AgentResult> _lastWorkerResult = new();
    public List<AgentPrompt> ReceivedPrompts { get; } = new();

    public MockAgentRunner ForColumn(string columnName, AgentResult result)
    {
        if (!_columnQueues.ContainsKey(columnName))
            _columnQueues[columnName] = new Queue<AgentResult>();
        _columnQueues[columnName].Enqueue(result);
        return this;
    }

    public Task<AgentResult> RunAsync(AgentPrompt prompt, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ReceivedPrompts.Add(prompt);

        // Determiner phase (StreamJson=false): auto-generate routing from worker result
        if (!prompt.StreamJson)
        {
            if (_lastWorkerResult.TryGetValue(prompt.ColumnName, out var workerResult))
            {
                if (workerResult.TimedOut)
                    return Task.FromResult(AgentResultBuilder.Timeout());

                if (workerResult.StructuredOutput is null)
                    return Task.FromResult(AgentResultBuilder.InvalidOutput());

                var json = workerResult.StructuredOutput.RawJson;
                return Task.FromResult(new AgentResult
                {
                    FullOutput       = $"<<<STRUCTURED_OUTPUT>>>\n{json}\n<<<END_STRUCTURED_OUTPUT>>>",
                    StructuredOutput = workerResult.StructuredOutput,
                    TimedOut         = false,
                });
            }
            // No prior worker result — return forward as safe default
            return Task.FromResult(AgentResultBuilder.Forward());
        }

        // Worker phase (StreamJson=true): use configured result
        if (_columnQueues.TryGetValue(prompt.ColumnName, out var queue) && queue.Count > 0)
        {
            var result = queue.Dequeue();
            _lastWorkerResult[prompt.ColumnName] = result;
            return Task.FromResult(result);
        }

        throw new InvalidOperationException(
            $"No mock response configured for column '{prompt.ColumnName}'. " +
            $"Add one with .ForColumn(\"{prompt.ColumnName}\", AgentResultBuilder.*)");
    }

    // Only returns worker prompts (StreamJson=true); determiner prompts are internal plumbing
    public AgentPrompt LastPromptFor(string columnName) =>
        ReceivedPrompts.LastOrDefault(p => p.ColumnName == columnName && p.StreamJson)
        ?? throw new InvalidOperationException($"No prompt was received for column '{columnName}'.");

    public IEnumerable<AgentPrompt> AllPromptsFor(string columnName) =>
        ReceivedPrompts.Where(p => p.ColumnName == columnName && p.StreamJson);
}
