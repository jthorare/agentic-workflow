using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>Reads the per-launch failopen.log (sandbox spec §3.1, §3.6): each HOOK-FAILOPEN line is a hook that let a call through because it broke.</summary>
public static class FailOpenLog
{
    private const int MaxHookChars = 80;

    /// <summary>Reads the lines of the log.</summary>
    /// <param name="lines">The log lines; bad lines are skipped.</param>
    /// <returns>One observation per line, targeting the hook name.</returns>
    public static IReadOnlyList<DenialObservation> Read(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        List<DenialObservation> result = [];
        var seen = 0;
        foreach (var line in lines)
        {
            if (++seen > TranscriptText.MaxLines || result.Count >= TranscriptText.MaxObservations)
            {
                break;
            }

            using var doc = TranscriptText.Parse(line);
            if (doc is null || TranscriptText.Str(doc.RootElement, "event") != "HOOK-FAILOPEN" || TranscriptText.Str(doc.RootElement, "hook") is not { Length: > 0 } hook)
            {
                continue;
            }

            if (hook.Length > MaxHookChars || !hook.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or ':' or '-'))
            {
                continue;
            }

            result.Add(new DenialObservation(DenialKind.Policy, hook, DenialRules.HookFailOpen));
        }

        return result;
    }
}
