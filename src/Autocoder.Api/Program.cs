using Autocoder.Api.Hubs;
using Autocoder.Api.Services;
using Autocoder.Core.Data;
using Autocoder.Core.Enums;
using Autocoder.Core.Interfaces;
using Autocoder.Core.Models;
using Autocoder.Orchestrator;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AutocoderDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

builder.Services.AddSingleton<PromptBuilder>();
builder.Services.AddSingleton<IAgentRunner, ClaudeCliRunner>();
builder.Services.AddSingleton<IGitService, GitService>();
builder.Services.AddSingleton<IRunningTaskRegistry, RunningTaskRegistry>();
builder.Services.AddScoped<IOrchestrator, OrchestratorService>();

builder.Services.AddHostedService<BackgroundOrchestratorService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AutocoderDbContext>();
    db.Database.EnsureCreated();

    // Schema migrations for new columns/tables (idempotent)
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS ColumnShellCommands (
            Id TEXT NOT NULL,
            ColumnId TEXT NOT NULL,
            Command TEXT NOT NULL,
            WorkingDirectory TEXT NULL,
            Position INTEGER NOT NULL DEFAULT 0,
            CONSTRAINT PK_ColumnShellCommands PRIMARY KEY (Id),
            CONSTRAINT FK_ColumnShellCommands_Columns_ColumnId FOREIGN KEY (ColumnId) REFERENCES Columns (Id) ON DELETE CASCADE
        )
    """);

    try { db.Database.ExecuteSqlRaw("ALTER TABLE Columns ADD COLUMN AgentEnabled INTEGER NOT NULL DEFAULT 1"); }
    catch { /* column already exists */ }

    try { db.Database.ExecuteSqlRaw("ALTER TABLE Boards ADD COLUMN MaxInProgress INTEGER NULL"); }
    catch { /* column already exists */ }

    try { db.Database.ExecuteSqlRaw("ALTER TABLE Boards ADD COLUMN CavemanMode INTEGER NOT NULL DEFAULT 0"); }
    catch { /* column already exists */ }

    try { db.Database.ExecuteSqlRaw("ALTER TABLE ColumnShellCommands ADD COLUMN Phase INTEGER NOT NULL DEFAULT 1"); }
    catch { /* column already exists */ }

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS TaskRepositories (
            Id TEXT NOT NULL,
            TaskId TEXT NOT NULL,
            RepositoryId TEXT NOT NULL,
            BranchName TEXT NOT NULL,
            IsEnabled INTEGER NOT NULL DEFAULT 1,
            CONSTRAINT PK_TaskRepositories PRIMARY KEY (Id),
            CONSTRAINT FK_TaskRepositories_WorkTasks_TaskId FOREIGN KEY (TaskId) REFERENCES WorkTasks (Id) ON DELETE CASCADE,
            CONSTRAINT FK_TaskRepositories_Repositories_RepositoryId FOREIGN KEY (RepositoryId) REFERENCES Repositories (Id) ON DELETE CASCADE
        )
    """);

    // Reset tasks that were Running when the server last shut down
    var stuckTasks = db.WorkTasks.Where(t => t.Status == WorkTaskStatus.Running).ToList();
    foreach (var t in stuckTasks)
    {
        t.Status = WorkTaskStatus.Waiting;
        t.UpdatedAt = DateTime.UtcNow;
        db.ContextEntries.Add(new ContextEntry
        {
            Id = Guid.NewGuid(),
            TaskId = t.Id,
            Kind = ContextEntryKind.SystemNote,
            Content = "Server restarted. Re-running this stage from the beginning.",
            CreatedAt = DateTime.UtcNow,
        });
    }
    if (stuckTasks.Count > 0) db.SaveChanges();

    var defaultBoardId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    if (!db.Boards.Any(b => b.Id == defaultBoardId))
    {
        var board = new Board
        {
            Id = defaultBoardId,
            Name = "Main Project",
            GlobalInstructions = "Always follow existing code conventions. Write clean, well-tested code.",
        };

        var spec = new Column
        {
            Id = Guid.NewGuid(), BoardId = defaultBoardId, Name = "Spec", Type = ColumnType.Agent,
            Position = 1, MaxAgentTurns = 10, TimeoutSeconds = 300, AutoForward = true,
            Instructions = """
                You are a senior engineer writing a technical specification. Do NOT write any code.
                Your job is to read the task, explore the existing codebase to understand the context,
                and produce a detailed written spec covering: what needs to change, which files are affected,
                what the approach should be, and any risks or edge cases. Be specific about file paths and
                function names. Output the spec as a markdown document. Do not implement anything.
                """
        };
        var inProgress = new Column
        {
            Id = Guid.NewGuid(), BoardId = defaultBoardId, Name = "In Progress", Type = ColumnType.Agent,
            Position = 2, MaxAgentTurns = 20, TimeoutSeconds = 600, AutoForward = true,
            Instructions = """
                You are a senior engineer implementing a task. Read the spec from the history above
                and implement exactly what is described. Write the code, create or modify files,
                run any necessary commands. Commit your changes with a descriptive message.
                Include the branch name you worked on in your structured output as "branchName".
                Do not over-engineer. Follow the existing code style.
                """
        };
        var review = new Column
        {
            Id = Guid.NewGuid(), BoardId = defaultBoardId, Name = "Code Review", Type = ColumnType.Agent,
            Position = 3, MaxAgentTurns = 10, TimeoutSeconds = 300, AutoForward = true,
            Instructions = """
                You are a senior engineer doing a code review. Read the spec and the implementation
                from the history above. Review the actual changed files. Check for: correctness,
                edge cases, security issues, code style, test coverage, and whether the spec was
                fully implemented. If the implementation is good, output action=forward with your
                assessment. If there are significant issues, output action=backward with a detailed
                list of problems that need to be fixed.
                """,
            BackwardTargetColumnId = Guid.Empty
        };
        var testing = new Column
        {
            Id = Guid.NewGuid(), BoardId = defaultBoardId, Name = "Testing", Type = ColumnType.Agent,
            Position = 4, MaxAgentTurns = 10, TimeoutSeconds = 300, AutoForward = true,
            Instructions = """
                You are a QA engineer. Run the existing test suite and verify the implementation
                works correctly. Check that: all existing tests still pass, the new functionality
                works as described in the spec, and edge cases are handled. If everything passes,
                output action=forward. If tests fail or behaviour is wrong, output action=backward
                with a clear description of what failed and why.
                """,
            BackwardTargetColumnId = Guid.Empty
        };

        review.BackwardTargetColumnId  = inProgress.Id;
        testing.BackwardTargetColumnId = inProgress.Id;

        db.Boards.Add(board);
        db.Columns.AddRange(
            new Column { Id = Guid.NewGuid(), BoardId = defaultBoardId, Name = "To Do", Type = ColumnType.Input, Position = 0 },
            spec, inProgress, review, testing,
            new Column { Id = Guid.NewGuid(), BoardId = defaultBoardId, Name = "Done",  Type = ColumnType.Input, Position = 5 }
        );
        db.SaveChanges();
    }
}

app.UseCors();
app.MapControllers();
app.MapHub<OrchestratorHub>("/hubs/orchestrator");

app.Run();
