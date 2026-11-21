using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Launch.Usage;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch.Usage;

/// <summary>#3420: the Claude Code reader's read state is kept on disk between runs, written only when it changed, and never stops a read.</summary>
[Lane(TestLane.Unit)]
public sealed class UsageReadCacheTests : IDisposable
{
    private static readonly DateTimeOffset _from = new(2026, 10, 17, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _to = _from.AddDays(1);
    private readonly UsageFixture _files = new();
    private readonly UsageFixture _saved = new();

    public void Dispose()
    {
        _files.Dispose();
        _saved.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Line(string id, long input) => UsageFixture.Claude(id, "2026-10-17T08:00:00.000Z", "claude-sonnet-4-5", input, 0, 0, 0);

    private ClaudeCodeUsage Reader(UsageReadCache cache) => new(_files.Root, [], cache);

    [Fact]
    public async Task A_pass_with_nothing_appended_writes_nothing()
    {
        _files.Write("-repo/a.jsonl", Line("m1", 10));
        var cache = new UsageReadCache(_saved.Root);
        var reader = Reader(cache);

        _ = await reader.Between(_from, _to, Token);
        var afterFirst = cache.Writes;
        _ = await reader.Between(_from, _to, Token);

        Expect.Equal((1, 1), (afterFirst, cache.Writes));
    }

    [Fact]
    public async Task An_append_after_a_restart_is_counted_once()
    {
        _files.Write("-repo/a.jsonl", Line("m1", 10));
        _ = await Reader(new UsageReadCache(_saved.Root)).Between(_from, _to, Token);

        _files.Write("-repo/a.jsonl", Line("m1", 10), Line("m2", 5));
        var restarted = await Reader(new UsageReadCache(_saved.Root)).Between(_from, _to, Token);

        Expect.Equal(15L, Assert.Single(restarted).Input);
    }

    [Fact]
    public async Task A_corrupt_cache_file_is_dropped_and_the_reader_reads_from_scratch()
    {
        _files.Write("-repo/a.jsonl", Line("m1", 10));
        await File.WriteAllTextAsync(Path.Combine(_saved.Root, "claude-code.db"), "this is not a database, only text", Token);

        var used = await Reader(new UsageReadCache(_saved.Root)).Between(_from, _to, Token);
        var again = await Reader(new UsageReadCache(_saved.Root)).Between(_from, _to, Token);

        Expect.Equal((10L, 10L), (Assert.Single(used).Input, Assert.Single(again).Input));
    }

    [Fact]
    public async Task A_transcript_that_is_gone_leaves_the_cache_at_the_next_save()
    {
        _files.Write("-repo/a.jsonl", Line("m1", 10));
        _files.Write("-repo/b.jsonl", Line("m2", 5));
        var reader = Reader(new UsageReadCache(_saved.Root));
        _ = await reader.Between(_from, _to, Token);
        Expect.Equal(2, new UsageReadCache(_saved.Root).Load("claude-code").Count);

        File.Delete(Path.Combine(_files.Root, "-repo", "b.jsonl"));
        _ = await reader.Between(_from, _to, Token);

        var left = new UsageReadCache(_saved.Root).Load("claude-code");
        Assert.EndsWith("a.jsonl", Assert.Single(left).Key, StringComparison.Ordinal);
        Expect.Equal(1, Assert.Single(left).Value.Messages.Count);
    }
}
