namespace Autocoder.Core.Orchestration;

public record ShellCommandResult(string Command, int ExitCode, string Stdout, string Stderr);
