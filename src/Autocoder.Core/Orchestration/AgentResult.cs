namespace Autocoder.Core.Orchestration;

public class AgentResult
{
    public string FullOutput { get; set; } = string.Empty;
    public AgentStructuredOutput? StructuredOutput { get; set; }
    public bool TimedOut { get; set; }
}
