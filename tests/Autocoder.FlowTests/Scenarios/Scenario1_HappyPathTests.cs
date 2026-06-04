using Autocoder.Core.Enums;
using Autocoder.FlowTests.Infrastructure;
using FluentAssertions;
using Moq;

namespace Autocoder.FlowTests.Scenarios;

/// <summary>
/// Scenario 1 — Happy Path: Full Forward Run
/// A task flows through every column without rework or questions.
/// </summary>
public class Scenario1_HappyPathTests : FlowTestBase
{
    [Fact]
    public async Task Task_flows_through_all_columns_without_rework()
    {
        var taskId = await CreateTaskAsync("Add CSV export button to the data table");

        // Spec → PendingApproval (AutoForward=false)
        MockAgent.ForColumn("In Specification", AgentResultBuilder.Forward("Spec written.", "feature/csv-export-button"));
        await Orchestrator.ProcessTaskAsync(taskId);

        var afterSpec = await GetTaskAsync(taskId);
        afterSpec.CurrentColumnId.Should().Be(InProgressColumnId, "spec forward moves task to In Progress");
        afterSpec.Status.Should().Be(WorkTaskStatus.PendingApproval, "In Specification has AutoForward=false");
        afterSpec.BranchName.Should().Be("autocoder/add-csv-export-button-to-the-data-table");

        // User approves spec
        await Orchestrator.ApproveTaskAsync(taskId);
        var afterApproval = await GetTaskAsync(taskId);
        afterApproval.Status.Should().Be(WorkTaskStatus.Waiting);

        // In Progress → Code Review (AutoForward=true, no gate)
        MockAgent.ForColumn("In Progress", AgentResultBuilder.Forward("Implementation complete."));
        await Orchestrator.ProcessTaskAsync(taskId);

        var afterInProgress = await GetTaskAsync(taskId);
        afterInProgress.CurrentColumnId.Should().Be(CodeReviewColumnId);
        afterInProgress.Status.Should().Be(WorkTaskStatus.Waiting, "In Progress has AutoForward=true");

        // Code Review → Testing (AutoForward=true)
        MockAgent.ForColumn("Code Review", AgentResultBuilder.Forward("No issues found."));
        await Orchestrator.ProcessTaskAsync(taskId);

        var afterCodeReview = await GetTaskAsync(taskId);
        afterCodeReview.CurrentColumnId.Should().Be(TestingColumnId);
        afterCodeReview.Status.Should().Be(WorkTaskStatus.Waiting, "Code Review has AutoForward=true");

        // Testing → Done (AutoForward=false, so PendingApproval first)
        MockAgent.ForColumn("Testing", AgentResultBuilder.Forward("All 47 tests pass."));
        await Orchestrator.ProcessTaskAsync(taskId);

        var afterTesting = await GetTaskAsync(taskId);
        afterTesting.CurrentColumnId.Should().Be(DoneColumnId);
        afterTesting.Status.Should().Be(WorkTaskStatus.PendingApproval, "Testing has AutoForward=false");

        // Final approval → Done
        await Orchestrator.ApproveTaskAsync(taskId);
        var final = await GetTaskAsync(taskId);
        final.Status.Should().Be(WorkTaskStatus.Done);
        final.CurrentColumnId.Should().Be(DoneColumnId);
    }

    [Fact]
    public async Task Context_chain_has_one_entry_per_agent_column_in_order()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");

        MockAgent.ForColumn("In Specification", AgentResultBuilder.Forward("Spec written.", "feature/csv-export-button"));
        await Orchestrator.ProcessTaskAsync(taskId);
        await Orchestrator.ApproveTaskAsync(taskId);

        MockAgent.ForColumn("In Progress", AgentResultBuilder.Forward("Implementation complete."));
        await Orchestrator.ProcessTaskAsync(taskId);

        MockAgent.ForColumn("Code Review", AgentResultBuilder.Forward("No issues."));
        await Orchestrator.ProcessTaskAsync(taskId);

        MockAgent.ForColumn("Testing", AgentResultBuilder.Forward("All tests pass."));
        await Orchestrator.ProcessTaskAsync(taskId);
        await Orchestrator.ApproveTaskAsync(taskId);

        var chain = await GetContextChainAsync(taskId);
        chain.Should().HaveCount(4);
        chain[0].ColumnName.Should().Be("In Specification");
        chain[1].ColumnName.Should().Be("In Progress");
        chain[2].ColumnName.Should().Be("Code Review");
        chain[3].ColumnName.Should().Be("Testing");
        chain.Should().AllSatisfy(e => e.Action.Should().Be(TransitionAction.Forward));
    }

    [Fact]
    public async Task Git_service_is_called_once_to_create_branch_after_spec()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");

        MockAgent.ForColumn("In Specification", AgentResultBuilder.Forward("Spec done.", "feature/csv-export-button"));
        await Orchestrator.ProcessTaskAsync(taskId);

        MockGit.Verify(
            g => g.SetupWorktreeAsync(
                It.Is<WorkTask>(t => t.BranchName == "autocoder/add-csv-export-button"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Branch_name_is_not_overwritten_on_subsequent_steps()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");

        MockAgent.ForColumn("In Specification", AgentResultBuilder.Forward("Spec done.", "feature/csv-export-button"));
        await Orchestrator.ProcessTaskAsync(taskId);
        await Orchestrator.ApproveTaskAsync(taskId);

        MockAgent.ForColumn("In Progress", AgentResultBuilder.Forward("Done."));
        await Orchestrator.ProcessTaskAsync(taskId);

        var task = await GetTaskAsync(taskId);
        task.BranchName.Should().Be("autocoder/add-csv-export-button");

        MockGit.Verify(
            g => g.SetupWorktreeAsync(It.IsAny<WorkTask>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
