using Autocoder.Core.Enums;
using Autocoder.FlowTests.Infrastructure;
using FluentAssertions;

namespace Autocoder.FlowTests.Scenarios;

/// <summary>
/// Scenario 5 — Agent Produces Invalid Structured Output
/// Agent response missing the sentinel block; task enters error state without transitioning.
/// </summary>
public class Scenario5_InvalidStructuredOutputTests : FlowTestBase
{
    [Fact]
    public async Task Invalid_output_sets_error_status_and_does_not_transition()
    {
        var taskId = await CreateTaskAsync("Refactor auth middleware");

        MockAgent.ForColumn("In Specification", AgentResultBuilder.InvalidOutput());
        await Orchestrator.ProcessTaskAsync(taskId);

        var task = await GetTaskAsync(taskId);
        task.Status.Should().Be(WorkTaskStatus.Error);
        task.CurrentColumnId.Should().Be(InSpecificationColumnId,
            "task must not transition on invalid output");
    }

    [Fact]
    public async Task Error_message_describes_missing_structured_output()
    {
        var taskId = await CreateTaskAsync("Refactor auth middleware");

        MockAgent.ForColumn("In Specification", AgentResultBuilder.InvalidOutput());
        await Orchestrator.ProcessTaskAsync(taskId);

        var task = await GetTaskAsync(taskId);
        task.ErrorMessage.Should().NotBeNullOrEmpty();
        task.ErrorMessage.Should().Contain("structured output",
            "error should explain what was missing");
    }

    [Fact]
    public async Task No_context_entry_is_saved_for_a_failed_run()
    {
        var taskId = await CreateTaskAsync("Refactor auth middleware");

        MockAgent.ForColumn("In Specification", AgentResultBuilder.InvalidOutput());
        await Orchestrator.ProcessTaskAsync(taskId);

        var chain = await GetContextChainAsync(taskId);
        chain.Should().BeEmpty("a failed run with no valid output must not append to the context chain");
    }

    [Fact]
    public async Task Retry_resets_error_and_adds_system_note_to_context()
    {
        var taskId = await CreateTaskAsync("Refactor auth middleware");

        MockAgent.ForColumn("In Specification", AgentResultBuilder.InvalidOutput());
        await Orchestrator.ProcessTaskAsync(taskId);

        // Configure a successful response for the retry
        MockAgent.ForColumn("In Specification", AgentResultBuilder.Forward("Spec complete.", "feature/auth-refactor"));
        await Orchestrator.RetryTaskAsync(taskId);

        var chain = await GetContextChainAsync(taskId);
        var systemNote = chain.FirstOrDefault(e => e.Kind == ContextEntryKind.SystemNote);
        systemNote.Should().NotBeNull("retry must add a system note to inform the agent the previous run failed");
        systemNote!.Content.Should().Contain("WORK SUMMARY");

        var task = await GetTaskAsync(taskId);
        task.Status.Should().Be(WorkTaskStatus.PendingApproval, "successful retry should advance normally");
        task.CurrentColumnId.Should().Be(InProgressColumnId);
    }

    [Fact]
    public async Task Retry_prompt_contains_the_system_note()
    {
        var taskId = await CreateTaskAsync("Refactor auth middleware");

        MockAgent.ForColumn("In Specification", AgentResultBuilder.InvalidOutput());
        await Orchestrator.ProcessTaskAsync(taskId);

        MockAgent.ForColumn("In Specification", AgentResultBuilder.Forward("Spec.", "feature/auth-refactor"));
        await Orchestrator.RetryTaskAsync(taskId);

        var retryPrompt = MockAgent.AllPromptsFor("In Specification").Last().Content;
        retryPrompt.Should().Contain("WORK SUMMARY",
            "retry prompt must include the system note reminding the agent to write a clear work summary");
    }

    [Fact]
    public async Task Cannot_retry_a_task_that_is_not_in_error_state()
    {
        var taskId = await CreateTaskAsync("Refactor auth middleware");

        var act = async () => await Orchestrator.RetryTaskAsync(taskId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not in error*");
    }
}
