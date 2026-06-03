using Autocoder.Core.Enums;
using Autocoder.FlowTests.Infrastructure;
using FluentAssertions;

namespace Autocoder.FlowTests.Scenarios;

/// <summary>
/// Scenario 4 — Multi-round Ask
/// Agent asks two separate questions before deciding; task stays in Testing throughout.
/// </summary>
public class Scenario4_MultiRoundAskTests : FlowTestBase
{
    [Fact]
    public async Task Agent_can_ask_multiple_questions_before_producing_a_final_action()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");
        await RunSpecAndApproveAsync(taskId, "feature/csv-export-button");
        await RunInProgressAsync(taskId);
        await RunCodeReviewPassAsync(taskId);

        // Testing round 1: first question
        MockAgent.ForColumn("Testing", AgentResultBuilder.Ask("Should soft-deleted rows be included?"));
        await Orchestrator.ProcessTaskAsync(taskId);

        var afterQ1 = await GetTaskAsync(taskId);
        afterQ1.Status.Should().Be(WorkTaskStatus.Asking);
        afterQ1.CurrentColumnId.Should().Be(TestingColumnId);

        // Testing round 2: second question (triggered by SubmitAnswerAsync calling ProcessTaskAsync)
        MockAgent.ForColumn("Testing", AgentResultBuilder.Ask("Should timestamps use UTC or local timezone?"));
        await Orchestrator.SubmitAnswerAsync(taskId, "Exclude soft-deleted rows.");

        var afterQ2 = await GetTaskAsync(taskId);
        afterQ2.Status.Should().Be(WorkTaskStatus.Asking);
        afterQ2.CurrentColumnId.Should().Be(TestingColumnId, "still in Testing after second question");
        afterQ2.PendingQuestion.Should().Contain("UTC");

        // Testing round 3: final forward (triggered by second SubmitAnswerAsync)
        MockAgent.ForColumn("Testing", AgentResultBuilder.Forward("Both clarified. All tests pass."));
        await Orchestrator.SubmitAnswerAsync(taskId, "UTC timestamps.");

        var afterForward = await GetTaskAsync(taskId);
        afterForward.CurrentColumnId.Should().Be(DoneColumnId);
        afterForward.Status.Should().Be(WorkTaskStatus.PendingApproval);

        await Orchestrator.ApproveTaskAsync(taskId);
        var final = await GetTaskAsync(taskId);
        final.Status.Should().Be(WorkTaskStatus.Done);
    }

    [Fact]
    public async Task Context_chain_has_two_ask_and_two_answer_entries_before_final_forward()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");
        await RunSpecAndApproveAsync(taskId, "feature/csv-export-button");
        await RunInProgressAsync(taskId);
        await RunCodeReviewPassAsync(taskId);

        MockAgent.ForColumn("Testing", AgentResultBuilder.Ask("Question 1?"));
        await Orchestrator.ProcessTaskAsync(taskId);

        MockAgent.ForColumn("Testing", AgentResultBuilder.Ask("Question 2?"));
        await Orchestrator.SubmitAnswerAsync(taskId, "Answer 1.");

        MockAgent.ForColumn("Testing", AgentResultBuilder.Forward("Tests pass."));
        await Orchestrator.SubmitAnswerAsync(taskId, "Answer 2.");

        var chain = await GetContextChainAsync(taskId);
        var testingAndAnswerEntries = chain
            .Where(e => e.ColumnId == TestingColumnId || e.Kind == ContextEntryKind.UserAnswer)
            .OrderBy(e => e.CreatedAt)
            .ToList();

        testingAndAnswerEntries.Should().HaveCount(5);
        testingAndAnswerEntries[0].Action.Should().Be(TransitionAction.Ask,       "first Testing run: ask Q1");
        testingAndAnswerEntries[1].Kind.Should().Be(ContextEntryKind.UserAnswer,   "answer to Q1");
        testingAndAnswerEntries[2].Action.Should().Be(TransitionAction.Ask,       "second Testing run: ask Q2");
        testingAndAnswerEntries[3].Kind.Should().Be(ContextEntryKind.UserAnswer,   "answer to Q2");
        testingAndAnswerEntries[4].Action.Should().Be(TransitionAction.Forward,   "third Testing run: forward");
    }

    [Fact]
    public async Task Third_testing_run_prompt_includes_both_qa_pairs()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");
        await RunSpecAndApproveAsync(taskId, "feature/csv-export-button");
        await RunInProgressAsync(taskId);
        await RunCodeReviewPassAsync(taskId);

        MockAgent.ForColumn("Testing", AgentResultBuilder.Ask("Should soft-deleted rows be included?"));
        await Orchestrator.ProcessTaskAsync(taskId);

        MockAgent.ForColumn("Testing", AgentResultBuilder.Ask("Should timestamps use UTC?"));
        await Orchestrator.SubmitAnswerAsync(taskId, "Exclude soft-deleted rows.");

        MockAgent.ForColumn("Testing", AgentResultBuilder.Forward("Tests pass."));
        await Orchestrator.SubmitAnswerAsync(taskId, "UTC timestamps.");

        var thirdPrompt = MockAgent.AllPromptsFor("Testing").Last().Content;
        thirdPrompt.Should().Contain("soft-deleted rows");
        thirdPrompt.Should().Contain("Exclude soft-deleted rows.");
        thirdPrompt.Should().Contain("UTC");
        thirdPrompt.Should().Contain("UTC timestamps.");
    }

    [Fact]
    public async Task Testing_column_is_visited_exactly_three_times()
    {
        var taskId = await CreateTaskAsync("Add CSV export button");
        await RunSpecAndApproveAsync(taskId, "feature/csv-export-button");
        await RunInProgressAsync(taskId);
        await RunCodeReviewPassAsync(taskId);

        MockAgent.ForColumn("Testing", AgentResultBuilder.Ask("Q1?"));
        await Orchestrator.ProcessTaskAsync(taskId);

        MockAgent.ForColumn("Testing", AgentResultBuilder.Ask("Q2?"));
        await Orchestrator.SubmitAnswerAsync(taskId, "A1.");

        MockAgent.ForColumn("Testing", AgentResultBuilder.Forward("Pass."));
        await Orchestrator.SubmitAnswerAsync(taskId, "A2.");

        MockAgent.AllPromptsFor("Testing").Should().HaveCount(3);
    }
}
