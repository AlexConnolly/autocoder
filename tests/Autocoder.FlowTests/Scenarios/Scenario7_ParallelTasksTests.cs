using Autocoder.Core.Enums;
using Autocoder.FlowTests.Infrastructure;
using FluentAssertions;
using Moq;

namespace Autocoder.FlowTests.Scenarios;

/// <summary>
/// Scenario 7 — Parallel Tasks, No Cross-Contamination
/// Two tasks run simultaneously on different branches; state must remain isolated.
/// </summary>
public class Scenario7_ParallelTasksTests : FlowTestBase
{
    [Fact]
    public async Task Two_tasks_produce_independent_branch_names()
    {
        var taskAId = await CreateTaskAsync("Add CSV export");
        var taskBId = await CreateTaskAsync("Fix login redirect bug");

        MockAgent
            .ForColumn("In Specification", AgentResultBuilder.Forward("Spec A done.", "feature/csv-export"))
            .ForColumn("In Specification", AgentResultBuilder.Forward("Spec B done.", "fix/login-redirect"));

        // Run both specs sequentially (parallel scheduling is the orchestrator's concern;
        // here we verify the state isolation that parallel execution depends on)
        await Orchestrator.ProcessTaskAsync(taskAId);
        await Orchestrator.ProcessTaskAsync(taskBId);

        var taskA = await GetTaskAsync(taskAId);
        var taskB = await GetTaskAsync(taskBId);

        taskA.BranchName.Should().Be("autocoder/add-csv-export");
        taskB.BranchName.Should().Be("autocoder/fix-login-redirect-bug");
    }

    [Fact]
    public async Task Each_task_gets_its_own_git_worktree_setup_call()
    {
        var taskAId = await CreateTaskAsync("Add CSV export");
        var taskBId = await CreateTaskAsync("Fix login redirect bug");

        MockAgent
            .ForColumn("In Specification", AgentResultBuilder.Forward("Spec A.", "feature/csv-export"))
            .ForColumn("In Specification", AgentResultBuilder.Forward("Spec B.", "fix/login-redirect"));

        await Orchestrator.ProcessTaskAsync(taskAId);
        await Orchestrator.ProcessTaskAsync(taskBId);

        MockGit.Verify(
            g => g.SetupWorktreeAsync(It.IsAny<WorkTask>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        MockGit.Verify(
            g => g.SetupWorktreeAsync(
                It.Is<WorkTask>(t => t.BranchName == "autocoder/add-csv-export"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        MockGit.Verify(
            g => g.SetupWorktreeAsync(
                It.Is<WorkTask>(t => t.BranchName == "autocoder/fix-login-redirect-bug"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Each_task_has_an_independent_context_chain()
    {
        var taskAId = await CreateTaskAsync("Add CSV export");
        var taskBId = await CreateTaskAsync("Fix login redirect bug");

        MockAgent
            .ForColumn("In Specification", AgentResultBuilder.Forward("Spec for CSV export.", "feature/csv-export"))
            .ForColumn("In Specification", AgentResultBuilder.Forward("Spec for login fix.", "fix/login-redirect"));

        await Orchestrator.ProcessTaskAsync(taskAId);
        await Orchestrator.ApproveTaskAsync(taskAId);
        await Orchestrator.ProcessTaskAsync(taskBId);
        await Orchestrator.ApproveTaskAsync(taskBId);

        MockAgent
            .ForColumn("In Progress", AgentResultBuilder.Forward("CSV endpoint added."))
            .ForColumn("In Progress", AgentResultBuilder.Forward("Redirect logic fixed."));

        await Orchestrator.ProcessTaskAsync(taskAId);
        await Orchestrator.ProcessTaskAsync(taskBId);

        var chainA = await GetContextChainAsync(taskAId);
        var chainB = await GetContextChainAsync(taskBId);

        chainA.Should().HaveCount(2);
        chainB.Should().HaveCount(2);

        chainA.Should().AllSatisfy(e => e.TaskId.Should().Be(taskAId));
        chainB.Should().AllSatisfy(e => e.TaskId.Should().Be(taskBId));
    }

    [Fact]
    public async Task Task_a_failure_does_not_affect_task_b()
    {
        var taskAId = await CreateTaskAsync("Add CSV export");
        var taskBId = await CreateTaskAsync("Fix login redirect bug");

        // Task A fails in Spec
        MockAgent.ForColumn("In Specification", AgentResultBuilder.InvalidOutput());
        await Orchestrator.ProcessTaskAsync(taskAId);

        // Task B must still be able to run normally
        MockAgent.ForColumn("In Specification", AgentResultBuilder.Forward("Spec B.", "fix/login-redirect"));
        await Orchestrator.ProcessTaskAsync(taskBId);

        var taskA = await GetTaskAsync(taskAId);
        var taskB = await GetTaskAsync(taskBId);

        taskA.Status.Should().Be(WorkTaskStatus.Error);
        taskB.Status.Should().Be(WorkTaskStatus.PendingApproval);
        taskB.BranchName.Should().Be("autocoder/fix-login-redirect-bug");
    }

    [Fact]
    public async Task Tasks_in_parallel_produce_correct_statuses_independently()
    {
        var taskAId = await CreateTaskAsync("Add CSV export");
        var taskBId = await CreateTaskAsync("Fix login redirect bug");

        MockAgent
            .ForColumn("In Specification", AgentResultBuilder.Forward("Spec A.", "feature/csv-export"))
            .ForColumn("In Specification", AgentResultBuilder.Forward("Spec B.", "fix/login-redirect"));

        // Simulate concurrent processing
        await Task.WhenAll(
            Orchestrator.ProcessTaskAsync(taskAId),
            Orchestrator.ProcessTaskAsync(taskBId));

        var taskA = await GetTaskAsync(taskAId);
        var taskB = await GetTaskAsync(taskBId);

        taskA.Status.Should().Be(WorkTaskStatus.PendingApproval);
        taskB.Status.Should().Be(WorkTaskStatus.PendingApproval);
        taskA.BranchName.Should().NotBe(taskB.BranchName);
    }
}
