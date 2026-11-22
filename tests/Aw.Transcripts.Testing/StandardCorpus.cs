using Aw.Transcripts.Core;
using Aw.Transcripts.Core.Refs;
using Aw.Transcripts.Core.Sessions;
using Aw.Transcripts.Core.Turns;

namespace Aw.Transcripts.Testing;

/// <summary>
/// The corpus <see cref="TranscriptCorpusContract"/> expects, by session name: 'main' (two prompts), 'fork' (a resume
/// of main that copied its first exchange, then one prompt of its own), 'sub' (a subagent of main) and 'old' (written
/// ten days before <see cref="Start"/>). All are Claude Code sessions in one project directory, cwd /work/alpha.
/// The line numbers are the lines of the files the file binding writes.
/// </summary>
public static class StandardCorpus
{
    /// <summary>When main's first prompt was sent.</summary>
    public static readonly DateTimeOffset Start = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    /// <summary>The cwd every standard session ran in (no such directory exists, so it is also the repo).</summary>
    public const string Repo = "/work/alpha";

    /// <summary>The session names, newest file first.</summary>
    public static IReadOnlyList<string> Names { get; } = ["fork", "main", "sub", "old"];

    /// <summary>The standard transcripts.</summary>
    /// <param name="refOf">The ref of a session, by name, as the binding's corpus gives it.</param>
    /// <returns>The transcripts, as ReadSession returns them.</returns>
    public static IReadOnlyList<SessionTranscript> Transcripts(Func<string, SessionRef> refOf)
    {
        ArgumentNullException.ThrowIfNull(refOf);
        var main = refOf("main");
        var fork = refOf("fork");
        var sub = refOf("sub");
        var old = refOf("old");
        return
        [
            Transcript(fork, Start, Start.AddHours(1), subagent: false, parent: null,
                Human(fork, 6, Start.AddMinutes(40), "ship it"),
                Assistant(fork, 7, Start.AddMinutes(41), "Shipped.", "Bash")),
            Transcript(main, Start, Start.AddMinutes(30), subagent: false, parent: null,
                Human(main, 2, Start, "fix the login bug"),
                Assistant(main, 3, Start.AddMinutes(1), "Looking at it.\nFixed.", "Read", "Edit"),
                Human(main, 7, Start.AddMinutes(5), "now the tests"),
                Assistant(main, 8, Start.AddMinutes(6), "Tests pass.")),
            Transcript(sub, Start.AddMinutes(2), Start.AddMinutes(20), subagent: true, parent: main,
                Assistant(sub, 2, Start.AddMinutes(3), "Found it.", "Grep")),
            Transcript(old, Start.AddDays(-10), Start.AddDays(-10).AddMinutes(5), subagent: false, parent: null,
                Human(old, 1, Start.AddDays(-10), "an old prompt")),
        ];
    }

    private static SessionTranscript Transcript(SessionRef session, DateTimeOffset started, DateTimeOffset written, bool subagent, SessionRef? parent, params TranscriptTurn[] turns) =>
        new(new TranscriptSession(session, TranscriptProvider.ClaudeCode, started, written, Repo, subagent, parent, turns.Count(t => t.Role == TurnRole.Human)), turns, 0);

    private static TranscriptTurn Human(SessionRef session, int line, DateTimeOffset at, string text) =>
        new(TranscriptRefs.ForTurn(session, line), TurnRole.Human, at, text, []);

    private static TranscriptTurn Assistant(SessionRef session, int line, DateTimeOffset at, string text, params string[] tools) =>
        new(TranscriptRefs.ForTurn(session, line), TurnRole.Assistant, at, text, tools);
}
