using Autocoder.Core.Interfaces;
using Autocoder.Core.Orchestration;

namespace Autocoder.FlowTests.Infrastructure;

public class MockAgentRunner : IAgentRunner
{
    private readonly Dictionary<string, Queue<AgentResult>> _columnQueues = new();
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

        if (_columnQueues.TryGetValue(prompt.ColumnName, out var queue) && queue.Count > 0)
            return Task.FromResult(queue.Dequeue());

        throw new InvalidOperationException(
            $"No mock response configured for column '{prompt.ColumnName}'. " +
            $"Add one with .ForColumn(\"{prompt.ColumnName}\", AgentResultBuilder.*)");
    }

    public AgentPrompt LastPromptFor(string columnName) =>
        ReceivedPrompts.LastOrDefault(p => p.ColumnName == columnName)
        ?? throw new InvalidOperationException($"No prompt was received for column '{columnName}'.");

    public IEnumerable<AgentPrompt> AllPromptsFor(string columnName) =>
        ReceivedPrompts.Where(p => p.ColumnName == columnName);
}
