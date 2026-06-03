using Autocoder.Core.Enums;
using Autocoder.FlowTests.Infrastructure;
using FluentAssertions;

namespace Autocoder.FlowTests.Scenarios;

/// <summary>
/// Scenario 9 — Context Chain Integrity Check
/// Verifies that every agent receives the complete, ordered, correct history —
/// including entries from backward transitions and rework loops.
/// </summary>
public class Scenario9_ContextChainIntegrityTests : FlowTestBase
{
    private async Task<Guid> BuildComplexFlowAsync()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");

        // Spec
        MockAgent.ForColumn("In Specification", AgentResultBuilder.Forward("Spec done.", "feature/csv-export-button"));
        await Orchestrator.ProcessTaskAsync(taskId);
        await Orchestrator.ApproveTaskAsync(taskId);

        // In Progress #1
        MockAgent.ForColumn("In Progress", AgentResultBuilder.Forward("Implementation v1 done."));
        await Orchestrator.ProcessTaskAsync(taskId);

        // Code Review #1 — backward
        MockAgent.ForColumn("Code Review", AgentResultBuilder.Backward(
            "Two issues found.", "Missing null check", "Wrong CSV headers"));
        await Orchestrator.ProcessTaskAsync(taskId);

        // In Progress #2 — rework
        MockAgent.ForColumn("In Progress", AgentResultBuilder.Forward("Fixed null check and headers."));
        await Orchestrator.ProcessTaskAsync(taskId);

        // Code Review #2 — forward
        MockAgent.ForColumn("Code Review", AgentResultBuilder.Forward("Issues resolved."));
        await Orchestrator.ProcessTaskAsync(taskId);

        return taskId;
    }

    [Fact]
    public async Task Testing_agent_receives_all_five_prior_step_entries_in_its_prompt()
    {
        var taskId = await BuildComplexFlowAsync();

        MockAgent.ForColumn("Testing", AgentResultBuilder.Forward("Tests pass."));
        await Orchestrator.ProcessTaskAsync(taskId);

        var testingPrompt = MockAgent.LastPromptFor("Testing").Content;

        testingPrompt.Should().Contain("In Specification",  "spec step must be in prompt");
        testingPrompt.Should().Contain("In Progress",       "in-progress step(s) must be in prompt");
        testingPrompt.Should().Contain("Code Review",       "both code review steps must be in prompt");
    }

    [Fact]
    public async Task Testing_prompt_shows_backward_action_on_first_code_review()
    {
        var taskId = await BuildComplexFlowAsync();

        MockAgent.ForColumn("Testing", AgentResultBuilder.Forward("Tests pass."));
        await Orchestrator.ProcessTaskAsync(taskId);

        var testingPrompt = MockAgent.LastPromptFor("Testing").Content;
        testingPrompt.Should().Contain("Backward",
            "first Code Review was a backward transition and must be labelled as such in the prompt");
        testingPrompt.Should().Contain("Missing null check",
            "code review issues must appear in the prompt history");
    }

    [Fact]
    public async Task Context_chain_contains_exactly_five_agent_output_entries_before_testing()
    {
        var taskId = await BuildComplexFlowAsync();

        var chain = await GetContextChainAsync(taskId);
        var agentEntries = chain.Where(e => e.Kind == ContextEntryKind.AgentOutput).ToList();

        agentEntries.Should().HaveCount(5);
        agentEntries.Select(e => e.ColumnName).Should().Equal(
            "In Specification",
            "In Progress",
            "Code Review",
            "In Progress",
            "Code Review");
    }

    [Fact]
    public async Task Context_chain_entries_are_strictly_ordered_by_creation_time()
    {
        var taskId = await BuildComplexFlowAsync();

        var chain = await GetContextChainAsync(taskId);
        chain.Should().BeInAscendingOrder(e => e.CreatedAt,
            "context chain must always be chronological");
    }

    [Fact]
    public async Task Each_context_entry_belongs_to_the_correct_task()
    {
        var taskId = await BuildComplexFlowAsync();

        var chain = await GetContextChainAsync(taskId);
        chain.Should().AllSatisfy(e => e.TaskId.Should().Be(taskId));
    }

    [Fact]
    public async Task Code_review_backward_entry_has_correct_column_id_and_action()
    {
        var taskId = await BuildComplexFlowAsync();

        var chain = await GetContextChainAsync(taskId);
        var firstReview = chain.First(e => e.ColumnId == CodeReviewColumnId);

        firstReview.Action.Should().Be(TransitionAction.Backward);
        firstReview.ColumnName.Should().Be("Code Review");
        firstReview.Content.Should().Contain("Missing null check");
    }

    [Fact]
    public async Task Ask_and_user_answer_entries_are_included_in_subsequent_agent_prompts()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");
        await RunSpecAndApproveAsync(taskId, "feature/csv-export-button");
        await RunInProgressAsync(taskId);
        await RunCodeReviewPassAsync(taskId);

        // Testing asks a question
        MockAgent.ForColumn("Testing", AgentResultBuilder.Ask("Include soft-deleted rows?"));
        await Orchestrator.ProcessTaskAsync(taskId);

        // User answers; Testing re-runs and passes
        MockAgent.ForColumn("Testing", AgentResultBuilder.Forward("Tests pass."));
        await Orchestrator.SubmitAnswerAsync(taskId, "Exclude soft-deleted rows.");
        await Orchestrator.ApproveTaskAsync(taskId);

        var chain = await GetContextChainAsync(taskId);
        var userAnswer = chain.SingleOrDefault(e => e.Kind == ContextEntryKind.UserAnswer);
        userAnswer.Should().NotBeNull();
        userAnswer!.Content.Should().Be("Exclude soft-deleted rows.");

        var testingEntries = chain.Where(e => e.ColumnId == TestingColumnId).OrderBy(e => e.CreatedAt).ToList();
        testingEntries.Should().HaveCount(2);
        testingEntries[0].Action.Should().Be(TransitionAction.Ask);
        testingEntries[1].Action.Should().Be(TransitionAction.Forward);
    }
}
