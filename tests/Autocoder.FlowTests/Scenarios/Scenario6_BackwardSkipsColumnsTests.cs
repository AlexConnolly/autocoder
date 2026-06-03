using Autocoder.Core.Enums;
using Autocoder.FlowTests.Infrastructure;
using FluentAssertions;

namespace Autocoder.FlowTests.Scenarios;

/// <summary>
/// Scenario 6 — Backward Transition Skips Multiple Columns
/// Testing failure routes to In Progress directly (not Code Review),
/// verifying BackwardTargetColumnId overrides positional logic.
/// After fix: normal forward path resumes from In Progress's next column.
/// </summary>
public class Scenario6_BackwardSkipsColumnsTests : FlowTestBase
{
    [Fact]
    public async Task Testing_backward_routes_to_in_progress_not_code_review()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");
        await RunSpecAndApproveAsync(taskId, "feature/csv-export-button");
        await RunInProgressAsync(taskId);
        await RunCodeReviewPassAsync(taskId);

        MockAgent.ForColumn("Testing", AgentResultBuilder.Backward(
            "Integration tests fail. API returns 500 on empty dataset.",
            "Unhandled exception in export route when result set is empty"));
        await Orchestrator.ProcessTaskAsync(taskId);

        var task = await GetTaskAsync(taskId);
        task.CurrentColumnId.Should().Be(InProgressColumnId,
            "Testing's BackwardTargetColumnId is In Progress, not Code Review");
        task.CurrentColumnId.Should().NotBe(CodeReviewColumnId);
        task.Status.Should().Be(WorkTaskStatus.Waiting);
    }

    [Fact]
    public async Task After_fix_task_resumes_normal_forward_path_from_in_progress()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");
        await RunSpecAndApproveAsync(taskId, "feature/csv-export-button");
        await RunInProgressAsync(taskId);
        await RunCodeReviewPassAsync(taskId);

        // Testing fails → back to In Progress
        MockAgent.ForColumn("Testing", AgentResultBuilder.Backward("500 on empty dataset.", "Fix the null case"));
        await Orchestrator.ProcessTaskAsync(taskId);

        // In Progress fixes the bug → should go to Code Review next (not Testing)
        MockAgent.ForColumn("In Progress", AgentResultBuilder.Forward("Fixed null exception."));
        await Orchestrator.ProcessTaskAsync(taskId);

        var afterFix = await GetTaskAsync(taskId);
        afterFix.CurrentColumnId.Should().Be(CodeReviewColumnId,
            "after In Progress, next column in pipeline is Code Review");
        afterFix.Status.Should().Be(WorkTaskStatus.Waiting, "Code Review auto-forward=true");
    }

    [Fact]
    public async Task Task_completes_after_backward_fix_and_full_forward_run()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");
        await RunSpecAndApproveAsync(taskId, "feature/csv-export-button");
        await RunInProgressAsync(taskId);
        await RunCodeReviewPassAsync(taskId);

        // Testing fails
        MockAgent.ForColumn("Testing", AgentResultBuilder.Backward("Failing tests.", "Fix null case"));
        await Orchestrator.ProcessTaskAsync(taskId);

        // In Progress rework → Code Review → Testing → Done
        MockAgent.ForColumn("In Progress", AgentResultBuilder.Forward("Fixed."));
        await Orchestrator.ProcessTaskAsync(taskId);

        MockAgent.ForColumn("Code Review", AgentResultBuilder.Forward("Looks good."));
        await Orchestrator.ProcessTaskAsync(taskId);

        MockAgent.ForColumn("Testing", AgentResultBuilder.Forward("All tests pass now."));
        await Orchestrator.ProcessTaskAsync(taskId);
        await Orchestrator.ApproveTaskAsync(taskId);

        var final = await GetTaskAsync(taskId);
        final.Status.Should().Be(WorkTaskStatus.Done);
    }

    [Fact]
    public async Task Context_chain_shows_correct_column_sequence_with_skip()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");
        await RunSpecAndApproveAsync(taskId, "feature/csv-export-button");
        await RunInProgressAsync(taskId);
        await RunCodeReviewPassAsync(taskId);

        MockAgent.ForColumn("Testing", AgentResultBuilder.Backward("Fail.", "Fix null"));
        await Orchestrator.ProcessTaskAsync(taskId);

        MockAgent.ForColumn("In Progress", AgentResultBuilder.Forward("Fixed."));
        await Orchestrator.ProcessTaskAsync(taskId);

        MockAgent.ForColumn("Code Review", AgentResultBuilder.Forward("Good."));
        await Orchestrator.ProcessTaskAsync(taskId);

        MockAgent.ForColumn("Testing", AgentResultBuilder.Forward("Pass."));
        await Orchestrator.ProcessTaskAsync(taskId);
        await Orchestrator.ApproveTaskAsync(taskId);

        var chain = await GetContextChainAsync(taskId);
        chain.Select(e => e.ColumnName).Should().Equal(
            "In Specification",
            "In Progress",
            "Code Review",
            "Testing",
            "In Progress",
            "Code Review",
            "Testing");

        chain[3].Action.Should().Be(TransitionAction.Backward, "first Testing run was backward");
        chain[6].Action.Should().Be(TransitionAction.Forward,  "second Testing run was forward");
    }
}
