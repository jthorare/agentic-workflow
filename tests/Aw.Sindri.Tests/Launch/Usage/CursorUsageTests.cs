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
}
