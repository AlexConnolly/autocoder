using Autocoder.Core.Enums;
using Autocoder.FlowTests.Infrastructure;
using FluentAssertions;

namespace Autocoder.FlowTests.Scenarios;

/// <summary>
/// Scenario 3 — Agent Asks a Question During Testing
/// Testing agent is uncertain; pauses to ask user before deciding pass/fail.
/// </summary>
public class Scenario3_AskDuringTestingTests : FlowTestBase
{
    private async Task<Guid> SetupTaskInTestingAsync()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");
        await RunSpecAndApproveAsync(taskId, "feature/csv-export-button");
        await RunInProgressAsync(taskId);
        await RunCodeReviewPassAsync(taskId);
        return taskId;
    }

    [Fact]
    public async Task Ask_action_sets_status_to_asking_and_does_not_transition_column()
    {
        var taskId = await SetupTaskInTestingAsync();

        MockAgent.ForColumn("Testing", AgentResultBuilder.Ask(
            "Should soft-deleted rows be included or excluded from the CSV export?"));
        await Orchestrator.ProcessTaskAsync(taskId);

        var task = await GetTaskAsync(taskId);
        task.Status.Should().Be(WorkTaskStatus.Asking);
        task.CurrentColumnId.Should().Be(TestingColumnId, "task must stay in Testing — no column transition on ask");
        task.PendingQuestion.Should().Contain("soft-deleted rows");
    }

    [Fact]
    public async Task User_answer_is_saved_to_context_chain_as_user_answer_entry()
    {
        var taskId = await SetupTaskInTestingAsync();

        MockAgent.ForColumn("Testing", AgentResultBuilder.Ask("Should soft-deleted rows be included?"));
        await Orchestrator.ProcessTaskAsync(taskId);

        // Configure the re-run response before submitting answer (SubmitAnswerAsync calls ProcessTaskAsync internally)
        MockAgent.ForColumn("Testing", AgentResultBuilder.Forward("Clarification received. Tests pass."));
        await Orchestrator.SubmitAnswerAsync(taskId, "Exclude soft-deleted rows.");

        var chain = await GetContextChainAsync(taskId);
        var userAnswer = chain.FirstOrDefault(e => e.Kind == ContextEntryKind.UserAnswer);
        userAnswer.Should().NotBeNull();
        userAnswer!.Content.Should().Be("Exclude soft-deleted rows.");
    }

    [Fact]
    public async Task After_ask_and_answer_agent_reruns_same_column_and_can_go_backward()
    {
        var taskId = await SetupTaskInTestingAsync();

        MockAgent.ForColumn("Testing", AgentResultBuilder.Ask("Should soft-deleted rows be included?"));
        await Orchestrator.ProcessTaskAsync(taskId);

        // Testing re-run: after user's answer, agent discovers a bug and goes backward
        MockAgent.ForColumn("Testing", AgentResultBuilder.Backward(
            "Export includes soft-deleted rows. Fix required.",
            "Filter WHERE deleted_at IS NULL"));
        await Orchestrator.SubmitAnswerAsync(taskId, "Exclude soft-deleted rows. Deleted rows are hidden in the UI.");

        var task = await GetTaskAsync(taskId);
        task.CurrentColumnId.Should().Be(InProgressColumnId,
            "Testing backward target is In Progress, not Code Review");
        task.Status.Should().Be(WorkTaskStatus.Waiting);
    }

    [Fact]
    public async Task Context_chain_order_is_ask_then_user_answer_then_second_agent_run()
    {
        var taskId = await SetupTaskInTestingAsync();

        MockAgent.ForColumn("Testing", AgentResultBuilder.Ask("Include soft-deleted rows?"));
        await Orchestrator.ProcessTaskAsync(taskId);

        MockAgent.ForColumn("Testing", AgentResultBuilder.Backward("Bug found.", "Missing WHERE clause"));
        await Orchestrator.SubmitAnswerAsync(taskId, "Exclude soft-deleted rows.");

        var chain = await GetContextChainAsync(taskId);
        var testingEntries = chain.Where(e => e.ColumnId == TestingColumnId || e.Kind == ContextEntryKind.UserAnswer).ToList();

        testingEntries.Should().HaveCount(3);
        testingEntries[0].Kind.Should().Be(ContextEntryKind.AgentOutput);
        testingEntries[0].Action.Should().Be(TransitionAction.Ask);
        testingEntries[1].Kind.Should().Be(ContextEntryKind.UserAnswer);
        testingEntries[2].Kind.Should().Be(ContextEntryKind.AgentOutput);
        testingEntries[2].Action.Should().Be(TransitionAction.Backward);
    }

    [Fact]
    public async Task Second_testing_run_prompt_includes_the_question_and_user_answer()
    {
        var taskId = await SetupTaskInTestingAsync();

        MockAgent.ForColumn("Testing", AgentResultBuilder.Ask("Include soft-deleted rows?"));
        await Orchestrator.ProcessTaskAsync(taskId);

        MockAgent.ForColumn("Testing", AgentResultBuilder.Forward("Tests pass."));
        await Orchestrator.SubmitAnswerAsync(taskId, "Exclude soft-deleted rows.");

        var secondPrompt = MockAgent.AllPromptsFor("Testing").Last().Content;
        secondPrompt.Should().Contain("Include soft-deleted rows?", "question must be in context");
        secondPrompt.Should().Contain("Exclude soft-deleted rows.", "user answer must be in context");
    }
}
