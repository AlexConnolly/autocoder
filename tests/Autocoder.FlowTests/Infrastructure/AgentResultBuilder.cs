using System.Text.Json;
using Autocoder.Core.Enums;
using Autocoder.Core.Orchestration;

namespace Autocoder.FlowTests.Infrastructure;

public static class AgentResultBuilder
{
    public static AgentResult Forward(string summary = "Complete", string? branchName = null)
    {
        var data = new Dictionary<string, object?> { ["action"] = "forward", ["summary"] = summary };
        if (branchName is not null) data["branchName"] = branchName;
        return Build(TransitionAction.Forward, summary, null, data);
    }

    public static AgentResult Backward(string summary, params string[] issues)
    {
        var data = new Dictionary<string, object?> { ["action"] = "backward", ["summary"] = summary, ["issues"] = issues };
        return Build(TransitionAction.Backward, summary, null, data);
    }

    public static AgentResult Ask(string question)
    {
        var data = new Dictionary<string, object?> { ["action"] = "ask", ["question"] = question, ["summary"] = "Awaiting clarification" };
        return Build(TransitionAction.Ask, "Awaiting clarification", question, data);
    }

    public static AgentResult InvalidOutput() =>
        new() { FullOutput = "Some text but no structured output block.", StructuredOutput = null, TimedOut = false };

    public static AgentResult Timeout() =>
        new() { FullOutput = string.Empty, StructuredOutput = null, TimedOut = true };

    private static AgentResult Build(TransitionAction action, string summary, string? question, Dictionary<string, object?> data)
    {
        var rawJson = JsonSerializer.Serialize(data);
        var fullOutput = $"Agent completed work.\n<<<STRUCTURED_OUTPUT>>>\n{rawJson}\n<<<END_STRUCTURED_OUTPUT>>>";
        return new AgentResult
        {
            FullOutput = fullOutput,
            TimedOut = false,
            StructuredOutput = new AgentStructuredOutput
            {
                Action = action,
                Summary = summary,
                Question = question,
                RawJson = rawJson
            }
        };
    }
}
