using Autocoder.Core.Data;
using Autocoder.Core.Enums;
using Autocoder.Core.Interfaces;
using Autocoder.Core.Models;
using Autocoder.Orchestrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Autocoder.FlowTests.Infrastructure;

public abstract class FlowTestBase : IAsyncLifetime
{
    protected AutocoderDbContext Db { get; private set; } = null!;
    protected MockAgentRunner MockAgent { get; } = new MockAgentRunner();
    protected Mock<IGitService> MockGit { get; } = new Mock<IGitService>();
    protected IOrchestrator Orchestrator { get; private set; } = null!;

    protected Guid BoardId { get; private set; }
    protected Guid TodoColumnId { get; private set; }
    protected Guid InSpecificationColumnId { get; private set; }
    protected Guid InProgressColumnId { get; private set; }
    protected Guid CodeReviewColumnId { get; private set; }
    protected Guid TestingColumnId { get; private set; }
    protected Guid DoneColumnId { get; private set; }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AutocoderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        Db = new AutocoderDbContext(options);
        await Db.Database.EnsureCreatedAsync();
        await SeedBoardAsync();

        MockGit.Setup(g => g.SetupWorktreeAsync(It.IsAny<WorkTask>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        MockGit.Setup(g => g.TeardownWorktreeAsync(It.IsAny<WorkTask>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        MockGit.Setup(g => g.PushAndMergeAsync(It.IsAny<WorkTask>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(true);

        var mockRegistry = new Mock<IRunningTaskRegistry>();
        mockRegistry.Setup(r => r.CancelAndWaitAsync(It.IsAny<Guid>(), It.IsAny<TimeSpan>())).ReturnsAsync(true);
        mockRegistry.Setup(r => r.IsRegistered(It.IsAny<Guid>())).Returns(false);
        Orchestrator = new OrchestratorService(Db, MockAgent, MockGit.Object, new PromptBuilder(), mockRegistry.Object, NullLogger<OrchestratorService>.Instance);
    }

    public Task DisposeAsync() => Db.DisposeAsync().AsTask();

    private async Task SeedBoardAsync()
    {
        BoardId = Guid.NewGuid();
        TodoColumnId = Guid.NewGuid();
        InSpecificationColumnId = Guid.NewGuid();
        InProgressColumnId = Guid.NewGuid();
        CodeReviewColumnId = Guid.NewGuid();
        TestingColumnId = Guid.NewGuid();
        DoneColumnId = Guid.NewGuid();

        var board = new Board
        {
            Id = BoardId,
            Name = "Main Project",
            GlobalInstructions = "Always follow existing code conventions. Write TypeScript. Use the project's existing test framework.",
            Repositories = new List<BoardRepository>
            {
                new BoardRepository { Id = Guid.NewGuid(), BoardId = BoardId, Name = "api",      LocalPath = @"C:\repos\api",      DefaultBranch = "main" },
                new BoardRepository { Id = Guid.NewGuid(), BoardId = BoardId, Name = "frontend", LocalPath = @"C:\repos\frontend", DefaultBranch = "main" }
            },
            Columns = new List<Column>
            {
                new Column { Id = TodoColumnId,            BoardId = BoardId, Name = "To Do",            Type = ColumnType.Input, Position = 0 },
                new Column { Id = InSpecificationColumnId, BoardId = BoardId, Name = "In Specification", Type = ColumnType.Agent, Position = 1, AutoForward = false,  TimeoutSeconds = 300, MaxAgentTurns = 10 },
                new Column { Id = InProgressColumnId,      BoardId = BoardId, Name = "In Progress",      Type = ColumnType.Agent, Position = 2, AutoForward = true,   TimeoutSeconds = 300, MaxAgentTurns = 20 },
                new Column { Id = CodeReviewColumnId,      BoardId = BoardId, Name = "Code Review",      Type = ColumnType.Agent, Position = 3, AutoForward = true,   TimeoutSeconds = 300, MaxAgentTurns = 10, BackwardTargetColumnId = InProgressColumnId },
                new Column { Id = TestingColumnId,         BoardId = BoardId, Name = "Testing",          Type = ColumnType.Agent, Position = 4, AutoForward = false,  TimeoutSeconds = 300, MaxAgentTurns = 10, BackwardTargetColumnId = InProgressColumnId },
                new Column { Id = DoneColumnId,            BoardId = BoardId, Name = "Done",             Type = ColumnType.Input, Position = 5 }
            }
        };

        Db.Boards.Add(board);
        await Db.SaveChangesAsync();
    }

    protected async Task<Guid> CreateTaskAsync(string title, string? description = null)
    {
        var task = new WorkTask
        {
            Id = Guid.NewGuid(),
            BoardId = BoardId,
            Title = title,
            Description = description,
            CurrentColumnId = InSpecificationColumnId,
            Status = WorkTaskStatus.Waiting,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Db.WorkTasks.Add(task);
        await Db.SaveChangesAsync();
        return task.Id;
    }

    protected async Task<WorkTask> GetTaskAsync(Guid taskId) =>
        await Db.WorkTasks.AsNoTracking().FirstAsync(t => t.Id == taskId);

    protected async Task<List<ContextEntry>> GetContextChainAsync(Guid taskId) =>
        await Db.ContextEntries.AsNoTracking()
            .Where(e => e.TaskId == taskId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

    protected async Task RunSpecAndApproveAsync(Guid taskId, string branchName = "feature/test-branch")
    {
        MockAgent.ForColumn("In Specification", AgentResultBuilder.Forward("Spec written.", branchName));
        await Orchestrator.ProcessTaskAsync(taskId);
        await Orchestrator.ApproveTaskAsync(taskId);
    }

    protected async Task RunInProgressAsync(Guid taskId, string summary = "Implementation complete.")
    {
        MockAgent.ForColumn("In Progress", AgentResultBuilder.Forward(summary));
        await Orchestrator.ProcessTaskAsync(taskId);
    }

    protected async Task RunCodeReviewPassAsync(Guid taskId)
    {
        MockAgent.ForColumn("Code Review", AgentResultBuilder.Forward("No issues found."));
        await Orchestrator.ProcessTaskAsync(taskId);
    }
}
