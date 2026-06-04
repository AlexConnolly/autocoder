using System.Text;
using Autocoder.Core.Enums;
using Autocoder.Core.Models;
using Autocoder.Core.Orchestration;

namespace Autocoder.Orchestrator;

public class PromptBuilder
{
    public AgentPrompt Build(WorkTask task, Column column, IReadOnlyList<Column> orderedColumns)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# SYSTEM PREAMBLE");
        sb.AppendLine($"Board: {task.Board.Name}");
        if (!string.IsNullOrEmpty(task.Board.GlobalInstructions))
            sb.AppendLine($"Instructions: {task.Board.GlobalInstructions}");

        if (task.Board.Repositories.Count > 0)
        {
            sb.AppendLine("\n## Repositories");
            foreach (var repo in task.Board.Repositories)
                sb.AppendLine($"- {repo.Name}: {repo.LocalPath} (branch: {repo.DefaultBranch})");
        }

        sb.AppendLine("\n# TASK");
        sb.AppendLine($"Title: {task.Title}");
        if (!string.IsNullOrEmpty(task.Description))
            sb.AppendLine($"Description: {task.Description}");
        if (!string.IsNullOrEmpty(task.BranchName))
            sb.AppendLine($"Working Branch: {task.BranchName}");

        var entries = task.ContextEntries.OrderBy(e => e.CreatedAt).ToList();
        if (entries.Count > 0)
        {
            sb.AppendLine("\n# HISTORY");
            foreach (var entry in entries)
            {
                switch (entry.Kind)
                {
                    case ContextEntryKind.AgentOutput:
                        sb.AppendLine($"\n## {entry.ColumnName} [{entry.Action}]");
                        sb.AppendLine(entry.Content);
                        break;
                    case ContextEntryKind.UserAnswer:
                        sb.AppendLine("\n## User Response");
                        sb.AppendLine(entry.Content);
                        break;
                    case ContextEntryKind.SystemNote:
                        sb.AppendLine("\n## System Note");
                        sb.AppendLine(entry.Content);
                        break;
                }
            }
        }

        sb.AppendLine($"\n# YOUR TASK: {column.Name}");
        if (!string.IsNullOrEmpty(column.Instructions))
            sb.AppendLine(column.Instructions);

        sb.AppendLine("\n## When finished");
        sb.AppendLine("Write a plain-text WORK SUMMARY covering:");
        sb.AppendLine("- What you did and what changed");
        sb.AppendLine("- Whether the work succeeded or has problems");
        sb.AppendLine("- Any git branch name you created or worked on");
        sb.AppendLine("- Any blockers or questions you have");
        sb.AppendLine("Do NOT output JSON. Just write the summary in plain text.");

        var workDir = task.WorktreePath
            ?? task.Board.Repositories.FirstOrDefault()?.LocalPath;

        return new AgentPrompt
        {
            ColumnId = column.Id,
            ColumnName = column.Name,
            Content = sb.ToString(),
            MaxTurns = column.MaxAgentTurns > 0 ? column.MaxAgentTurns : 10,
            TaskId = task.Id,
            BoardId = task.BoardId,
            WorktreePath = workDir,
        };
    }

    public AgentPrompt BuildDeterminer(WorkTask task, Column column, string workerOutput)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# WORKFLOW ROUTING DECISION");
        sb.AppendLine($"Task: {task.Title}");
        sb.AppendLine($"Stage just completed: {column.Name}");

        sb.AppendLine("\n## Stage instructions were:");
        if (!string.IsNullOrEmpty(column.Instructions))
            sb.AppendLine(column.Instructions);

        sb.AppendLine("\n## Work output from the agent:");
        sb.AppendLine(workerOutput);

        sb.AppendLine("\n## Your job");
        sb.AppendLine("Based ONLY on the work output text above, decide how this task should be routed.");
        sb.AppendLine("Do NOT use any tools. Do NOT read any files. Do NOT run any commands.");
        sb.AppendLine("Just read the summary text and output the JSON decision immediately.");
        sb.AppendLine();
        sb.AppendLine("Routing options:");
        sb.AppendLine("- forward: work is complete and successful for this stage");
        sb.AppendLine("- backward: significant problems found, needs to go back for fixes");
        sb.AppendLine("- ask: a question for the user is needed before proceeding");
        sb.AppendLine();
        sb.AppendLine("Your entire response must be exactly this JSON and nothing else:");
        sb.AppendLine("<<<STRUCTURED_OUTPUT>>>");
        sb.AppendLine("{");
        sb.AppendLine("  \"action\": \"forward\",");
        sb.AppendLine("  \"summary\": \"one sentence describing what was done\",");
        sb.AppendLine("  \"branchName\": \"branch-name-if-mentioned-or-omit-this-field\"");
        sb.AppendLine("}");
        sb.AppendLine("<<<END_STRUCTURED_OUTPUT>>>");

        var workDir = task.WorktreePath
            ?? task.Board.Repositories.FirstOrDefault()?.LocalPath;

        return new AgentPrompt
        {
            ColumnId = column.Id,
            ColumnName = column.Name,
            Content = sb.ToString(),
            MaxTurns = 8,
            TaskId = task.Id,
            BoardId = task.BoardId,
            WorktreePath = workDir,
            StreamJson = false,
        };
    }
}
