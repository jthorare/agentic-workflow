using Aw.Kernel.Testing;
using Aw.Transcripts.Core;
using Aw.Transcripts.Core.Refs;
using Aw.Transcripts.Core.Sessions;
using Aw.Transcripts.Tests.Fixtures;

namespace Aw.Transcripts.Tests.Providers;

[Lane(TestLane.Integration)]
public sealed class CursorTranscriptTests : IDisposable
{
    private static readonly DateTimeOffset _written = new(2026, 11, 3, 18, 0, 0, TimeSpan.Zero);
    private readonly TempRoots _roots = new();

    [Fact]
    public async Task A_transcript_reads_as_its_user_queries_timed_by_their_timestamp_blocks()
    {
        var path = _roots.Write("cursor/projects/work-gamma/agent-transcripts/c1/c1.jsonl", _written,
            CursorLines.User("fix it", "Tuesday, Nov 3, 2026, 9:15 AM (UTC-8)"),
            CursorLines.Assistant(text: null, "Shell"),
            CursorLines.Assistant("All done."),
            CursorLines.TurnEnded(),
            CursorLines.UserRaw("<system_notification>queued</system_notification>"),
            CursorLines.User("and the docs"));

        var read = (await _roots.Corpus().ReadSession(TranscriptRefs.ForFile(TranscriptProvider.Cursor, path), CancellationToken.None)).Value;

        var at = new DateTimeOffset(2026, 11, 3, 17, 15, 0, TimeSpan.Zero);
        Assert.Equal([$"Human {at:O} fix it", $"Assistant {at:O} All done. [Shell]", $"Human {at:O} and the docs"],
            read.Turns.Select(t => $"{t.Role} {t.At:O} {t.Text}{(t.Tools.Count > 0 ? $" [{string.Join(',', t.Tools)}]" : "")}"), StringComparer.Ordinal);
        Assert.Equal(at, read.Session.StartedAt);
        Assert.Null(read.Session.Repo);
        Assert.Equal(2, read.Session.HumanTurns);
    }

    [Fact]
    public async Task A_line_of_an_unknown_shape_is_counted_as_skipped()
    {
        var path = _roots.Write("cursor/projects/work-gamma/agent-transcripts/c2/c2.jsonl", _written,
            CursorLines.User("fix it"),
            """{"foo":1}""",
            """{"role":"user"}""",
            """{"role":"robot","message":{"content":"x"}}""",
            CursorLines.TurnEnded());

        var read = (await _roots.Corpus().ReadSession(TranscriptRefs.ForFile(TranscriptProvider.Cursor, path), CancellationToken.None)).Value;

        Assert.Equal(1, read.Session.HumanTurns);
        Assert.Equal(3, read.SkippedLines);
    }

    [Fact]
    public async Task A_subagent_transcript_has_no_human_turn_and_links_its_parent()
    {
        var parent = _roots.Write("cursor/projects/work-gamma/agent-transcripts/c1/c1.jsonl", _written, CursorLines.User("go"));
        _ = _roots.Write("cursor/projects/work-gamma/agent-transcripts/c1/subagents/s1.jsonl", _written, CursorLines.User("your task"), CursorLines.Assistant("ok"));

        var sessions = (await _roots.Corpus().ListSessions(new SessionQuery(_written.AddDays(-1)), CancellationToken.None)).Value;

        var sub = Assert.Single(sessions, s => s.IsSubagent);
        Assert.Equal(TranscriptRefs.ForFile(TranscriptProvider.Cursor, parent), sub.Parent);
        Assert.Equal(0, sub.HumanTurns);
        Assert.Equal(_written, sub.StartedAt);
    }

    public void Dispose() => _roots.Dispose();
}
