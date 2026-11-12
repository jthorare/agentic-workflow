using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Infrastructure.Launch.Usage;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch.Usage;

/// <summary>
/// The Cursor usage reader (API P10a Q11): built from the documented line shape, since no Cursor CLI was available to check it against. It stays unverified,
/// so a Cursor target is probed as limited and its spend is tokens only.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class CursorUsageTests : IDisposable
{
    private static readonly DateTimeOffset _from = new(2026, 10, 17, 7, 0, 0, TimeSpan.Zero);
    private readonly UsageFixture _files = new();

    public void Dispose() => _files.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lines_that_carry_usage_count_and_others_are_ignored()
    {
        _files.Write("proj/agent-transcripts/t1/t1.jsonl",
            """{"role":"assistant","timestamp":"2026-10-17T08:00:00.000Z","model":"auto","usage":{"input_tokens":10,"output_tokens":5}}""",
            """{"role":"user","message":{"content":"hi"}}""",
            """{"role":"assistant","timestamp":"2026-10-16T08:00:00.000Z","usage":{"input_tokens":99,"output_tokens":99}}""");

        var reader = new CursorUsage(_files.Root);
        var used = await reader.Between(_from, _from.AddDays(1), Token);

        Assert.False(reader.Verified);
        Expect.Equal(new ModelTokens("auto", 10, 0, 0, 5), Assert.Single(used));
    }

    [Fact]
    public async Task Transcripts_without_usage_are_no_usage()
    {
        _files.Write("proj/agent-transcripts/t1/t1.jsonl", """{"role":"user","message":{"content":"hi"}}""", """{"type":"turn_ended","status":"success"}""");

        Assert.Empty(await new CursorUsage(_files.Root).Between(_from, _from.AddDays(1), Token));
    }

    /// <summary>Pins #3129: a second read parses only the lines appended since the first. An already-read line is rewritten in place with the same length, so a reader that re-reads the file sees the new numbers.</summary>
    [Fact]
    public async Task A_second_read_parses_only_the_appended_lines()
    {
        var relative = "proj/agent-transcripts/t1/t1.jsonl";
        var path = Path.Combine(_files.Root, relative);
        var first = """{"role":"assistant","timestamp":"2026-10-17T08:00:00.000Z","model":"auto","usage":{"input_tokens":10,"output_tokens":5}}""";
        var sameLength = """{"role":"assistant","timestamp":"2026-10-17T08:00:00.000Z","model":"auto","usage":{"input_tokens":20,"output_tokens":7}}""";
        _files.Write(relative, first);
        var reader = new CursorUsage(_files.Root);
        Expect.Equal(new ModelTokens("auto", 10, 0, 0, 5), Assert.Single(await reader.Between(_from, _from.AddDays(1), Token)));

        Assert.Equal(first.Length, sameLength.Length);
        await File.WriteAllTextAsync(path, (await File.ReadAllTextAsync(path, Token)).Replace(first, sameLength, StringComparison.Ordinal), Token);
        await File.AppendAllLinesAsync(path, ["""{"role":"assistant","timestamp":"2026-10-17T09:00:00.000Z","model":"auto","usage":{"input_tokens":30,"output_tokens":6}}"""], Token);

        var again = await reader.Between(_from, _from.AddDays(1), Token);

        // the first line keeps the numbers read the first time; only the appended line is new
        Expect.Equal(new ModelTokens("auto", 10 + 30, 0, 0, 5 + 6), Assert.Single(again));
    }

    [Fact]
    public async Task A_transcript_rewritten_shorter_is_read_again_from_the_start()
    {
        var relative = "proj/agent-transcripts/t1/t1.jsonl";
        _files.Write(relative,
            """{"role":"assistant","timestamp":"2026-10-17T08:00:00.000Z","model":"auto","usage":{"input_tokens":10,"output_tokens":5}}""",
            """{"role":"assistant","timestamp":"2026-10-17T09:00:00.000Z","model":"auto","usage":{"input_tokens":30,"output_tokens":6}}""");
        var reader = new CursorUsage(_files.Root);
        _ = await reader.Between(_from, _from.AddDays(1), Token);

        _files.Write(relative, """{"timestamp":"2026-10-17T10:00:00.000Z","model":"m","usage":{"input_tokens":1,"output_tokens":2}}""");

        var again = await reader.Between(_from, _from.AddDays(1), Token);

        Assert.Equal<ModelTokens>(await new CursorUsage(_files.Root).Between(_from, _from.AddDays(1), Token), again);
        Expect.Equal(new ModelTokens("m", 1, 0, 0, 2), Assert.Single(again));
    }

    [Fact]
    public async Task A_second_read_after_an_append_equals_a_fresh_full_read()
    {
        var relative = "proj/agent-transcripts/t1/t1.jsonl";
        var path = Path.Combine(_files.Root, relative);
        _files.Write(relative, """{"role":"assistant","timestamp":"2026-10-17T08:00:00.000Z","model":"auto","usage":{"input_tokens":10,"output_tokens":5}}""");
        var reader = new CursorUsage(_files.Root);
        var first = await reader.Between(_from, _from.AddDays(1), Token);

        await File.AppendAllLinesAsync(path,
            [
                """{"role":"assistant","timestamp":"2026-10-17T09:00:00.000Z","model":"auto","usage":{"input_tokens":30,"output_tokens":6}}""",
                """{"role":"assistant","timestamp":"2026-10-17T10:00:00.000Z","model":"other","usage":{"input_tokens":1,"output_tokens":2}}""",
            ],
            Token);

        var again = await reader.Between(_from, _from.AddDays(1), Token);

        Assert.NotEqual<ModelTokens>(first, again);
        Assert.Equal<ModelTokens>(await new CursorUsage(_files.Root).Between(_from, _from.AddDays(1), Token), again);
        Assert.Equal(["auto", "other"], again.Select(static m => m.Model).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }
}
