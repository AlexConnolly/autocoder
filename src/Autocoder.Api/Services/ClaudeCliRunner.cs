using System.Diagnostics;
using System.Text;
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
        var outputBuilder = new StringBuilder();
        var args = $"--print --output-format text --max-turns {prompt.MaxTurns} --dangerously-skip-permissions";
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

        var group = $"board-{prompt.BoardId}";
        var taskIdStr = prompt.TaskId.ToString();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            outputBuilder.AppendLine(e.Data);
            _ = _hub.Clients.Group(group).SendAsync("LiveOutput", taskIdStr, e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                _logger.LogDebug("[claude stderr] {Line}", e.Data);
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.StandardInput.WriteAsync(prompt.Content);
            process.StandardInput.Close();

            await process.WaitForExitAsync(ct);

            var fullOutput = outputBuilder.ToString();
            _logger.LogDebug("Claude exited {Code}, output length {Len}", process.ExitCode, fullOutput.Length);

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
                FullOutput       = outputBuilder.ToString(),
                StructuredOutput = null,
                TimedOut         = true,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Claude process failed for task {TaskId}", prompt.TaskId);
            return new AgentResult
            {
                FullOutput       = outputBuilder.ToString(),
                StructuredOutput = null,
                TimedOut         = false,
            };
        }
    }
}
