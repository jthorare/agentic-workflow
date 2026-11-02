using System.Text.Json;

namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>Reads path candidates out of a Codex rollout tail (sandbox spec §15): Codex has no denied record, only the shell's error text.</summary>
public static class CodexTranscriptDenials
{
    private static readonly string[] _outputFields = ["output", "stderr", "aggregated_output", "stdout"];

    /// <summary>Reads the lines of a rollout.</summary>
    /// <param name="jsonlLines">The last lines of the rollout; bad lines are skipped.</param>
    /// <returns>The paths tool calls failed on (no policy denials).</returns>
    public static TranscriptDenials Read(IEnumerable<string> jsonlLines)
    {
        ArgumentNullException.ThrowIfNull(jsonlLines);
        List<string> paths = [];
        var seen = 0;
        foreach (var line in jsonlLines)
        {
            if (++seen > TranscriptText.MaxLines)
            {
                break;
            }

            using var doc = TranscriptText.Parse(line);
            if (doc is not null)
            {
                ReadRecord(doc.RootElement, paths);
            }
        }

        return new TranscriptDenials([], paths);
    }

    private static void ReadRecord(JsonElement root, List<string> paths)
    {
        if (TranscriptText.Obj(root, "payload") is not { } payload)
        {
            return;
        }

        var kind = (TranscriptText.Str(root, "type"), TranscriptText.Str(payload, "type"));
        if (kind is not (("response_item", "function_call_output") or ("event_msg", "exec_command_end") or ("event_msg", "patch_apply_end")) || Succeeded(payload))
        {
            return;
        }

        foreach (var field in _outputFields)
        {
            if (TranscriptText.Str(payload, field) is { } text)
            {
                TranscriptText.AddPaths(text, paths);
            }
        }
    }

    private static bool Succeeded(JsonElement payload) =>
        (payload.TryGetProperty("exit_code", out var code) && code.ValueKind == JsonValueKind.Number && code.TryGetInt32(out var n) && n == 0)
        || (payload.TryGetProperty("success", out var ok) && ok.ValueKind == JsonValueKind.True);
}
