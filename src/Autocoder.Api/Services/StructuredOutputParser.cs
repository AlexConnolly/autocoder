using System.Text.Json;
using Autocoder.Core.Enums;
using Autocoder.Core.Orchestration;

namespace Autocoder.Api.Services;

public static class StructuredOutputParser
{
    private const string StartSentinel = "<<<STRUCTURED_OUTPUT>>>";
    private const string EndSentinel   = "<<<END_STRUCTURED_OUTPUT>>>";

    public static AgentStructuredOutput? TryParse(string fullOutput)
    {
        // Try sentinel-delimited block first
        var startIdx = fullOutput.IndexOf(StartSentinel, StringComparison.Ordinal);
        if (startIdx >= 0)
        {
            var jsonStart = startIdx + StartSentinel.Length;
            var endIdx = fullOutput.IndexOf(EndSentinel, jsonStart, StringComparison.Ordinal);
            if (endIdx >= 0)
            {
                var json = fullOutput[jsonStart..endIdx].Trim();
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var result = TryParseJson(json);
                    if (result != null) return result;
                }
            }
        }

        // Fallback: scan for any JSON object containing an "action" field.
        // Claude often skips the sentinel wrappers and outputs raw JSON.
        return TryParseJsonBlock(fullOutput);
    }

    private static AgentStructuredOutput? TryParseJsonBlock(string text)
    {
        // Walk forward through every { and try to parse from there to the last }
        var lastClose = text.LastIndexOf('}');
        if (lastClose < 0) return null;

        var i = 0;
        while (i <= lastClose)
        {
            var open = text.IndexOf('{', i);
            if (open < 0 || open > lastClose) break;

            var candidate = text[open..(lastClose + 1)];
            var result = TryParseJson(candidate);
            if (result != null) return result;

            i = open + 1;
        }
        return null;
    }

    private static AgentStructuredOutput? TryParseJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("action", out _)) return null;

            var actionStr = root.TryGetProperty("action", out var ap) ? ap.GetString() : "forward";
            var action = (actionStr ?? "forward").ToLowerInvariant() switch
            {
                "backward" => TransitionAction.Backward,
                "ask"      => TransitionAction.Ask,
                _          => TransitionAction.Forward,
            };

            var summary  = root.TryGetProperty("summary",  out var sp) ? sp.GetString() ?? "" : "";
            var question = root.TryGetProperty("question", out var qp) ? qp.GetString() : null;

            return new AgentStructuredOutput
            {
                Action   = action,
                Summary  = summary,
                Question = question,
                RawJson  = json,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
