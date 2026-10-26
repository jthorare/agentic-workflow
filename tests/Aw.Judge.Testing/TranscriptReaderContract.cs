using System.Globalization;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Transcripts;
using Xunit;

namespace Aw.Judge.Testing;

/// <summary>
/// The ITranscriptReader contract: the same tests run against the in-memory fake and the Claude adapter
/// (.agents/rules/csharp.md, Seams). A derived class supplies a reader that holds the standard transcript
/// (tests/fixtures/judge-transcripts/standard.jsonl) and an empty one.
/// </summary>
public abstract class TranscriptReaderContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>What the standard transcript reads as (ModifiedAt is not part of the contract).</summary>
    public static readonly SessionId StandardSession = SessionId.From("sess-1");

    private static readonly DateTimeOffset _start = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);

    /// <summary>A reader that holds the standard transcript as the file of <see cref="StandardSession"/>.</summary>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The reader and the path of the file.</returns>
    protected abstract ValueTask<(ITranscriptReader Reader, string Path)> CreateWithStandard(CancellationToken cancellationToken);

    /// <summary>A reader with no transcripts (for a real adapter, over a directory that doesn't exist).</summary>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The reader.</returns>
    protected abstract ValueTask<ITranscriptReader> CreateEmpty(CancellationToken cancellationToken);

    /// <summary>The standard transcript as the contract expects it: ModifiedAt is a placeholder.</summary>
    /// <returns>The transcript.</returns>
    public static Transcript Standard() => new(
        _start,
        [
            new TranscriptPrompt(_start, "fix the login bug"),
            new TranscriptPrompt(_start.AddMinutes(5), "now fix\nthe tests"),
            new TranscriptPrompt(_start.AddMinutes(6), "hello"),
        ],
        [
            new TranscriptTurn(StandardSession, 0, "fix the login bug",
            [
                new TranscriptEdit("src/a.cs", "x = 1;", "x = 2;"),
                new TranscriptEdit("src/b.cs", "", "line1\nline2"),
                new TranscriptEdit("src/c.cs", "a", "b"),
                new TranscriptEdit("src/c.cs", "c", "d"),
            ]),
            new TranscriptTurn(StandardSession, 1, "now fix\nthe tests", []),
            new TranscriptTurn(SessionId.From("sess-2"), 0, "hello", []),
        ],
        new Dictionary<SessionId, DateTimeOffset>
        {
            [StandardSession] = _start.AddMinutes(7),
            [SessionId.From("sess-2")] = _start.AddMinutes(6),
        });

    /// <summary>A session's transcript is found by its id and holds its real prompts, turns with their edits, and session ends.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_session_is_found_by_its_id()
    {
        var (reader, _) = await CreateWithStandard(_none);

        var found = await reader.FindSession(StandardSession, _none);

        Expect.That(found.IsSuccess, "a session with a transcript is found");
        AssertStandard(found.Value);
    }

    /// <summary>A transcript is read by path with the same result.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_transcript_is_read_by_its_path()
    {
        var (reader, path) = await CreateWithStandard(_none);

        var read = await reader.Read(path, _none);

        Expect.That(read.IsSuccess, "a transcript at a known path is read");
        AssertStandard(read.Value);
    }

    /// <summary>A session with no transcript is AW-JUDGE-015.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_unknown_session_has_no_transcript()
    {
        var (reader, _) = await CreateWithStandard(_none);

        var found = await reader.FindSession(SessionId.From("no-such-session"), _none);

        Expect.That(found.IsError && found.Error.Code == JudgeErrors.NoTranscript.Code, "an unknown session is AW-JUDGE-015");
    }

    /// <summary>A path that can't be read is AW-JUDGE-015.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_file_has_no_transcript()
    {
        var (reader, path) = await CreateWithStandard(_none);

        var read = await reader.Read(path + ".missing", _none);

        Expect.That(read.IsError && read.Error.Code == JudgeErrors.NoTranscript.Code, "a missing file is AW-JUDGE-015");
    }

    /// <summary>With no transcripts at all (no projects directory), every lookup is AW-JUDGE-015 and nothing throws.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_empty_reader_finds_nothing()
    {
        var reader = await CreateEmpty(_none);

        var found = await reader.FindSession(StandardSession, _none);

        Expect.That(found.IsError && found.Error.Code == JudgeErrors.NoTranscript.Code, "no projects directory is AW-JUDGE-015");
    }

    private static void AssertStandard(Transcript actual)
    {
        var expected = Standard();
        Expect.That(actual.Prompts.SequenceEqual(expected.Prompts), "only real prompts are prompts: machine text, tool results and compaction summaries are not, and a text-block prompt joins its blocks with a newline");
        Expect.That(actual.Turns.Count == expected.Turns.Count, "one turn per real prompt");
        for (var i = 0; i < expected.Turns.Count; i++)
        {
            var (want, got) = (expected.Turns[i], actual.Turns[i]);
            Expect.That(got.Session == want.Session && got.Index == want.Index && got.Prompt == want.Prompt, string.Create(CultureInfo.InvariantCulture, $"turn {i} keeps its session, its number in the session and its prompt"));
            Expect.That(got.Edits.SequenceEqual(want.Edits), string.Create(CultureInfo.InvariantCulture, $"turn {i} holds the Edit, Write and MultiEdit calls that followed its prompt, in order, and no other tool call"));
        }

        Expect.That(actual.SessionEnds.Count == expected.SessionEnds.Count && expected.SessionEnds.All(p => actual.SessionEnds.TryGetValue(p.Key, out var end) && end == p.Value),
            "each session ends at its last timestamped line, a line that isn't JSON being skipped");
    }
}
