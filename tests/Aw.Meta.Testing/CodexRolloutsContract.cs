using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The ICodexRollouts contract, run against the in-memory fake and the sessions-directory adapter.</summary>
public abstract class CodexRolloutsContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A fresh reader plus a way to put a session's transcript in place.</summary>
    /// <returns>The reader and the writer (session id, whole text).</returns>
    protected abstract (ICodexRollouts Reader, Action<string, string> Put) Create();

    /// <summary>A session with no transcript reads as null.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_session_with_no_transcript_has_no_tail()
    {
        var (reader, _) = Create();

        var tail = await reader.Tail(SessionId.From("nope"), 1000, _none);

        Expect.That(tail.IsSuccess && tail.Value is null, "no tail");
    }

    /// <summary>The lines of a short transcript read back whole.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_short_transcript_reads_back_whole()
    {
        var (reader, put) = Create();
        put("s-1", "one\ntwo\nthree\n");

        var tail = await reader.Tail(SessionId.From("s-1"), 1000, _none);

        Expect.That(tail.Value!.Lines.SequenceEqual(["one", "two", "three"], StringComparer.Ordinal), "all three lines");
    }

    /// <summary>Only the last bytes are read, and the line the window cuts is dropped.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_long_transcript_is_read_from_its_end_and_the_cut_line_is_dropped()
    {
        var (reader, put) = Create();
        put("s-2", "aaaaaaaaaa\nbbbbbbbbbb\ncccccccccc\n");

        var tail = await reader.Tail(SessionId.From("s-2"), 15, _none);

        Expect.That(tail.Value!.Lines.SequenceEqual(["cccccccccc"], StringComparer.Ordinal), "only the last whole line");
    }
}
