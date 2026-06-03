using Autocoder.Core.Enums;

namespace Autocoder.Core.Orchestration;

public class AgentStructuredOutput
{
    public TransitionAction Action { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? Question { get; set; }
    public string RawJson { get; set; } = "{}";

    public bool TryGetString(string key, out string? value)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(RawJson);
            if (doc.RootElement.TryGetProperty(key, out var prop))
            {
                value = prop.GetString();
                return value is not null;
            }
        }
        catch { }
        value = null;
        return false;
    }
}
