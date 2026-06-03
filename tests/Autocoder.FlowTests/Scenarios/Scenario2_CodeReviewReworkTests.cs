using Autocoder.Core.Enums;
using Autocoder.FlowTests.Infrastructure;
using FluentAssertions;

namespace Autocoder.FlowTests.Scenarios;

/// <summary>
/// Scenario 2 — Code Review Failure → Rework Loop → Done
/// Code review identifies issues; task returns to In Progress, is fixed, then passes.
/// </summary>
public class Scenario2_CodeReviewReworkTests : FlowTestBase
{
    [Fact]
    public async Task Code_review_failure_routes_task_back_to_in_progress()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");

        await RunSpecAndApproveAsync(taskId, "feature/csv-export-button");
        await RunInProgressAsync(taskId);

        // Code Review fails first attempt
        MockAgent.ForColumn("Code Review", AgentResultBuilder.Backward(
            "Two issues found.",
            "Missing null check on empty result set",
            "CSV headers use internal field names instead of display names"));
        await Orchestrator.ProcessTaskAsync(taskId);

        var afterReview = await GetTaskAsync(taskId);
        afterReview.CurrentColumnId.Should().Be(InProgressColumnId,
            "Code Review BackwardTargetColumnId is In Progress");
        afterReview.Status.Should().Be(WorkTaskStatus.Waiting);
    }

    [Fact]
    public async Task Task_reaches_done_after_rework_loop()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");

        await RunSpecAndApproveAsync(taskId, "feature/csv-export-button");
        await RunInProgressAsync(taskId);

        // Code Review fails
        MockAgent.ForColumn("Code Review", AgentResultBuilder.Backward(
            "Issues found.", "Missing null check", "Wrong header names"));
        await Orchestrator.ProcessTaskAsync(taskId);

        // In Progress fixes the issues
        MockAgent.ForColumn("In Progress", AgentResultBuilder.Forward("Fixed null check and headers."));
        await Orchestrator.ProcessTaskAsync(taskId);

        // Code Review passes second time
        MockAgent.ForColumn("Code Review", AgentResultBuilder.Forward("Issues resolved. Approved."));
        await Orchestrator.ProcessTaskAsync(taskId);

        // Testing passes
        MockAgent.ForColumn("Testing", AgentResultBuilder.Forward("All tests pass."));
        await Orchestrator.ProcessTaskAsync(taskId);
        await Orchestrator.ApproveTaskAsync(taskId);

        var final = await GetTaskAsync(taskId);
        final.Status.Should().Be(WorkTaskStatus.Done);
    }

    [Fact]
    public async Task Context_chain_records_both_code_review_visits_in_order()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");

        await RunSpecAndApproveAsync(taskId, "feature/csv-export-button");
        await RunInProgressAsync(taskId);

        MockAgent.ForColumn("Code Review", AgentResultBuilder.Backward("Issues found.", "Missing null check"));
        await Orchestrator.ProcessTaskAsync(taskId);

        MockAgent.ForColumn("In Progress", AgentResultBuilder.Forward("Fixed issues."));
        await Orchestrator.ProcessTaskAsync(taskId);

        MockAgent.ForColumn("Code Review", AgentResultBuilder.Forward("All clear."));
        await Orchestrator.ProcessTaskAsync(taskId);

        MockAgent.ForColumn("Testing", AgentResultBuilder.Forward("Pass."));
        await Orchestrator.ProcessTaskAsync(taskId);
        await Orchestrator.ApproveTaskAsync(taskId);

        var chain = await GetContextChainAsync(taskId);
        chain.Should().HaveCount(6);
        chain.Select(e => e.ColumnName).Should().Equal(
            "In Specification",
            "In Progress",
            "Code Review",
            "In Progress",
            "Code Review",
            "Testing");

        chain[2].Action.Should().Be(TransitionAction.Backward, "first code review was backward");
        chain[4].Action.Should().Be(TransitionAction.Forward, "second code review approved");
    }

    [Fact]
    public async Task Rework_agent_prompt_contains_code_review_feedback()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");

        await RunSpecAndApproveAsync(taskId, "feature/csv-export-button");
        await RunInProgressAsync(taskId);

        MockAgent.ForColumn("Code Review", AgentResultBuilder.Backward(
            "Issues found.", "Missing null check", "Wrong header names"));
        await Orchestrator.ProcessTaskAsync(taskId);

        MockAgent.ForColumn("In Progress", AgentResultBuilder.Forward("Fixed."));
        await Orchestrator.ProcessTaskAsync(taskId);

        var reworkPrompt = MockAgent.AllPromptsFor("In Progress").Last().Content;
        reworkPrompt.Should().Contain("Code Review", "rework prompt must include the review feedback");
        reworkPrompt.Should().Contain("Backward", "prompt should show the review action was backward");
        reworkPrompt.Should().Contain("Missing null check");
    }
}
