using Autocoder.Core.Enums;
using Autocoder.FlowTests.Infrastructure;
using FluentAssertions;

namespace Autocoder.FlowTests.Scenarios;

/// <summary>
/// Scenario 8 — Agent Timeout
/// Agent process exceeds column timeout; task enters error state without transitioning.
/// </summary>
public class Scenario8_AgentTimeoutTests : FlowTestBase
{
    [Fact]
    public async Task Timeout_sets_error_status_and_task_stays_in_current_column()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");
        await RunSpecAndApproveAsync(taskId, "feature/csv-export-button");

        MockAgent.ForColumn("In Progress", AgentResultBuilder.Timeout());
        await Orchestrator.ProcessTaskAsync(taskId);

        var task = await GetTaskAsync(taskId);
        task.Status.Should().Be(WorkTaskStatus.Error);
        task.CurrentColumnId.Should().Be(InProgressColumnId,
            "task must not transition when agent times out");
    }

    [Fact]
    public async Task Timeout_error_message_mentions_timeout()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");
        await RunSpecAndApproveAsync(taskId, "feature/csv-export-button");

        MockAgent.ForColumn("In Progress", AgentResultBuilder.Timeout());
        await Orchestrator.ProcessTaskAsync(taskId);

        var task = await GetTaskAsync(taskId);
        task.ErrorMessage.Should().NotBeNullOrEmpty();
        task.ErrorMessage.Should().Contain("timed out");
    }

    [Fact]
    public async Task No_context_entry_is_saved_for_a_timed_out_run()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");
        await RunSpecAndApproveAsync(taskId, "feature/csv-export-button");

        MockAgent.ForColumn("In Progress", AgentResultBuilder.Timeout());
        await Orchestrator.ProcessTaskAsync(taskId);

        var chain = await GetContextChainAsync(taskId);
        chain.Should().HaveCount(1, "only the spec entry; the timed-out run must not append anything");
        chain[0].ColumnName.Should().Be("In Specification");
    }

    [Fact]
    public async Task Retry_after_timeout_runs_the_same_column_again()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");
        await RunSpecAndApproveAsync(taskId, "feature/csv-export-button");

        MockAgent.ForColumn("In Progress", AgentResultBuilder.Timeout());
        await Orchestrator.ProcessTaskAsync(taskId);

        MockAgent.ForColumn("In Progress", AgentResultBuilder.Forward("Implementation complete."));
        await Orchestrator.RetryTaskAsync(taskId);

        var task = await GetTaskAsync(taskId);
        task.Status.Should().Be(WorkTaskStatus.Waiting, "Code Review auto-forward=true");
        task.CurrentColumnId.Should().Be(CodeReviewColumnId);
        task.ErrorMessage.Should().BeNull("error is cleared on successful retry");
    }

    [Fact]
    public async Task Timeout_does_not_affect_branch_name_set_by_earlier_step()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");
        await RunSpecAndApproveAsync(taskId, "feature/csv-export-button");

        MockAgent.ForColumn("In Progress", AgentResultBuilder.Timeout());
        await Orchestrator.ProcessTaskAsync(taskId);

        var task = await GetTaskAsync(taskId);
        task.BranchName.Should().Be("feature/csv-export-button",
            "timeout in a later step must not clear the branch set by the spec step");
    }

    [Fact]
    public async Task Multiple_timeouts_keep_task_in_error_state_until_retry()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");
        await RunSpecAndApproveAsync(taskId, "feature/csv-export-button");

        // First timeout
        MockAgent.ForColumn("In Progress", AgentResultBuilder.Timeout());
        await Orchestrator.ProcessTaskAsync(taskId);

        // Verify task is in error; cannot process again without retry
        var act = async () => await Orchestrator.ProcessTaskAsync(taskId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Waiting or Asking*");
    }
}
