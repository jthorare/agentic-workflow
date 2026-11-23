using System.Globalization;
using System.Text;
using Aw.Context.Core.Trust;
using Aw.Sindri.Host.Commands.Transcripts;
using Aw.Transcripts.Core;
using Aw.Transcripts.Core.Sessions;
using Aw.Transcripts.Core.Turns;

namespace Aw.Sindri.Host.Commands;

/// <summary>The text and JSON shapes of 'sindri transcripts list|show'. Transcript text is untrusted: a session's call gets it inside an &lt;untrusted source="transcript"&gt; fence.</summary>
internal static class TranscriptsText
{
    private const string Source = "transcript";

    internal static TranscriptSessionView View(TranscriptSession s) =>
        new(s.Ref.Value, TranscriptProviderNames.Of(s.Provider), s.StartedAt, s.LastWrittenAt, s.Repo, s.IsSubagent, s.Parent?.Value, s.HumanTurns);

    internal static TranscriptShowView View(SessionTranscript t, bool fence) =>
        new(View(t.Session), fence, t.SkippedLines, [.. t.Turns.Select(turn => new TranscriptTurnView(turn.Ref.Value, Role(turn.Role), turn.At, Body(turn.Text, fence), turn.Tools))]);

    /// <summary>One line per session, newest first: ref, provider, last written, human turns, repo, and the parent of a subagent.</summary>
    internal static string List(IReadOnlyList<TranscriptSession> sessions, DateTimeOffset since)
    {
        if (sessions.Count == 0)
        {
            return Invariant($"no sessions written since {Time(since)}\n");
        }

        var text = new StringBuilder();
        foreach (var s in sessions)
        {
            var turns = s.HumanTurns == 1 ? "1 human turn" : Invariant($"{s.HumanTurns} human turns");
            var sub = s.IsSubagent ? $"  subagent of {s.Parent?.Value ?? "an unknown session"}" : string.Empty;
            _ = text.AppendLine(Invariant($"{s.Ref.Value}  {TranscriptProviderNames.Of(s.Provider),-11}  {Time(s.LastWrittenAt)}  {turns,-14}  {Shown(s.Repo ?? "-")}{sub}"));
        }

        return text.ToString();
    }

    /// <summary>The session's header, then each turn: role, time and line, the tools of an assistant turn, and the text indented.</summary>
    internal static string Show(SessionTranscript t, bool fence)
    {
        var s = t.Session;
        var text = new StringBuilder(Invariant($"{s.Ref.Value}  {TranscriptProviderNames.Of(s.Provider)}  started {Time(s.StartedAt)}  last written {Time(s.LastWrittenAt)}  {s.HumanTurns} human  repo {Shown(s.Repo ?? "-")}\n"));
        if (s.IsSubagent)
        {
            _ = text.AppendLine($"subagent of {s.Parent?.Value ?? "an unknown session"}");
        }

        foreach (var turn in t.Turns)
        {
            var tools = turn.Tools.Count > 0 ? $"  tools: {Shown(string.Join(", ", turn.Tools))}" : string.Empty;
            _ = text.AppendLine().AppendLine(Invariant($"{Role(turn.Role)}  {Time(turn.At)}  #{turn.Ref.Line}{tools}"));
            foreach (var line in Shown(Body(turn.Text, fence)).Split('\n'))
            {
                _ = text.Append("  ").AppendLine(line);
            }
        }

        if (t.SkippedLines > 0)
        {
            _ = text.AppendLine().AppendLine(Invariant($"({t.SkippedLines} lines skipped: not a JSON object of the provider's shape)"));
        }

        return text.ToString();
    }

    // Control bytes (ESC, OSC 52, BEL) as \xNN so transcript text can't drive the terminal; newlines and tabs stay. Text output only: JSON escapes them itself.
    private static string Shown(string text) =>
        text.Any(static c => char.IsControl(c) && c is not '\n' and not '\t')
            ? string.Concat(text.Select(static c => char.IsControl(c) && c is not '\n' and not '\t' ? Invariant($"\\x{(int)c:x2}") : c.ToString()))
            : text;

    private static string Body(string text, bool fence) => fence ? UntrustedFence.Wrap(Source, author: null, text) : text;

    private static string Role(TurnRole role) => role == TurnRole.Human ? "human" : "assistant";

    private static string Time(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd HH:mm'Z'", CultureInfo.InvariantCulture);

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
