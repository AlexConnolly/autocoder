using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Autocoder.Api.Hubs;
using Autocoder.Core.Interfaces;
using Autocoder.Core.Orchestration;
using Microsoft.AspNetCore.SignalR;

namespace Autocoder.Api.Services;

public class ClaudeCliRunner : IAgentRunner
{
    private readonly string _cliPath;
    private readonly IHubContext<OrchestratorHub> _hub;
    private readonly ILogger<ClaudeCliRunner> _logger;

    public ClaudeCliRunner(
        IConfiguration config,
        IHubContext<OrchestratorHub> hub,
        ILogger<ClaudeCliRunner> logger)
    {
        _cliPath = config["Claude:CliPath"] ?? "claude";
        _hub     = hub;
        _logger  = logger;
    }

    public async Task<AgentResult> RunAsync(AgentPrompt prompt, CancellationToken ct = default)
    {
        var assistantText = new StringBuilder();
        var finalResult   = new string[1] { string.Empty };

        var outputFormat  = prompt.StreamJson ? "stream-json" : "text";
        var verbose       = prompt.StreamJson ? "--verbose" : "";
        var cavemanPrompt = prompt.CavemanMode
            ? "--append-system-prompt \"Caveman mode: extremely terse. No markdown. No explanations. Minimal output. Just do the task.\""
            : "";
        var args = $"--print --output-format {outputFormat} {verbose} --max-turns {prompt.MaxTurns} --dangerously-skip-permissions {cavemanPrompt}".Trim();
        var workDir = prompt.WorktreePath is not null && Directory.Exists(prompt.WorktreePath)
            ? prompt.WorktreePath
            : Directory.GetCurrentDirectory();

        var psi = new ProcessStartInfo
        {
            FileName               = _cliPath,
            Arguments              = args,
            RedirectStandardInput  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            WorkingDirectory       = workDir,
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var group     = $"board-{prompt.BoardId}";
        var taskIdStr = prompt.TaskId.ToString();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            if (prompt.StreamJson)
                HandleStreamLine(e.Data, assistantText, finalResult, group, taskIdStr);
            else
            {
                assistantText.AppendLine(e.Data);
                _ = _hub.Clients.Group(group).SendAsync("LiveOutput", taskIdStr, e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                _logger.LogWarning("[claude stderr] task={TaskId} {Line}", prompt.TaskId, e.Data);
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.StandardInput.WriteAsync(prompt.Content);
            process.StandardInput.Close();

            await process.WaitForExitAsync(ct);

            // Prefer the clean result event text; fall back to accumulated assistant text
            var fullOutput = !string.IsNullOrEmpty(finalResult[0])
                ? finalResult[0]
                : assistantText.ToString();

            _logger.LogInformation("Claude exited code={Code} output={Len}chars task={TaskId}", process.ExitCode, fullOutput.Length, prompt.TaskId);

            return new AgentResult
            {
                FullOutput       = fullOutput,
                StructuredOutput = StructuredOutputParser.TryParse(fullOutput),
                TimedOut         = false,
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Claude process cancelled for task {TaskId}", prompt.TaskId);
            try { process.Kill(entireProcessTree: true); } catch { }

            return new AgentResult
            {
                FullOutput       = assistantText.ToString(),
                StructuredOutput = null,
                TimedOut         = true,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Claude process failed for task {TaskId}", prompt.TaskId);
            return new AgentResult
            {
                FullOutput       = assistantText.ToString(),
                StructuredOutput = null,
                TimedOut         = false,
            };
        }
    }

    private void HandleStreamLine(
        string jsonLine,
        StringBuilder assistantText,
        string[] finalResult,
        string group,
        string taskIdStr)
    {
        try
        {
            using var doc     = JsonDocument.Parse(jsonLine);
            var root          = doc.RootElement;
            var eventType     = root.TryGetProperty("type", out var tp) ? tp.GetString() : null;

            switch (eventType)
            {
                case "assistant":
                    if (!root.TryGetProperty("message", out var msg)) break;
                    if (!msg.TryGetProperty("content",  out var content)) break;

                    foreach (var item in content.EnumerateArray())
                    {
                        var itemType = item.TryGetProperty("type", out var itp) ? itp.GetString() : null;

                        if (itemType == "text")
                        {
                            var text = item.TryGetProperty("text", out var textProp)
                                ? textProp.GetString() ?? string.Empty
                                : string.Empty;
                            if (string.IsNullOrWhiteSpace(text)) continue;

                            assistantText.AppendLine(text);
                            foreach (var line in text.Split('\n'))
                            {
                                if (!string.IsNullOrWhiteSpace(line))
                                    _ = _hub.Clients.Group(group).SendAsync("LiveOutput", taskIdStr, line);
                            }
                        }
                        else if (itemType == "tool_use")
                        {
                            var liveLine = FormatToolUseLine(item);
                            if (liveLine is not null)
                                _ = _hub.Clients.Group(group).SendAsync("LiveOutput", taskIdStr, liveLine);
                        }
                    }
                    break;

                case "result":
                    var result = root.TryGetProperty("result", out var rp) ? rp.GetString() : null;
                    if (!string.IsNullOrEmpty(result))
                        finalResult[0] = result;
                    break;
            }
        }
        catch (JsonException)
        {
            // Non-JSON line — shouldn't happen with stream-json but handle gracefully
            assistantText.AppendLine(jsonLine);
            _ = _hub.Clients.Group(group).SendAsync("LiveOutput", taskIdStr, jsonLine);
        }
    }

    private static string? FormatToolUseLine(JsonElement item)
    {
        var toolName = item.TryGetProperty("name", out var np) ? np.GetString() : "tool";
        if (!item.TryGetProperty("input", out var input)) return toolName;

        string? detail = null;
        if      (input.TryGetProperty("file_path",   out var fp))  detail = fp.GetString();
        else if (input.TryGetProperty("command",     out var cmd)) detail = cmd.GetString();
        else if (input.TryGetProperty("path",        out var p))   detail = p.GetString();
        else if (input.TryGetProperty("pattern",     out var pat)) detail = pat.GetString();
        else if (input.TryGetProperty("description", out var d))   detail = d.GetString();
        else if (input.TryGetProperty("prompt",      out var prm)) detail = prm.GetString()?.Split('\n').FirstOrDefault()?.Trim();

        return detail is not null ? $"{toolName}: {detail}" : toolName;
    }
}
