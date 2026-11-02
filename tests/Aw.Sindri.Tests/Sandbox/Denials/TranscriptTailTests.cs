using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Denials;

/// <summary>The best-effort tail reader (sandbox spec §3.6).</summary>
[Lane(TestLane.Unit)]
public sealed class TranscriptTailTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-tail-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Missing_file_reads_as_no_lines() => Assert.Empty(TranscriptTail.Read(Path.Combine(_dir, "nope.jsonl")));

    [Fact]
    public void Tail_drops_the_cut_first_line_and_keeps_the_end()
    {
        var file = Path.Combine(_dir, "t.jsonl");
        File.WriteAllText(file, "aaaaaaaaaa\nbbbbbbbbbb\ncccccccccc\n");

        Assert.Equal(["bbbbbbbbbb", "cccccccccc"], TranscriptTail.Read(file, maxBytes: 24));
        Assert.Equal(["aaaaaaaaaa", "bbbbbbbbbb", "cccccccccc"], TranscriptTail.Read(file));
    }
}
