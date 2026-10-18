using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Infrastructure.Launch.Usage;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch.Usage;

/// <summary>The Codex usage reader over fixture rollouts (never the real ~/.codex).</summary>
[Lane(TestLane.Unit)]
public sealed class CodexUsageTests : IDisposable
{
    private const string Id = "0198f2a1-0000-7000-8000-000000000001";
    private static readonly DateTimeOffset _from = new(2026, 10, 17, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _to = _from.AddDays(1);
    private readonly UsageFixture _files = new();

    public void Dispose() => _files.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Turns_inside_the_window_count_with_cached_input_split_out()
    {
        _files.Write($"2026/10/17/rollout-2026-10-17T08-00-00-{Id}.jsonl",
            UsageFixture.CodexModel("2026-10-17T08:00:00.000Z", "gpt-5"),
            UsageFixture.CodexTokens("2026-10-17T06:00:00.000Z", total: 5, input: 4, cached: 0, output: 1),   // before the window
            UsageFixture.CodexTokens("2026-10-17T08:00:01.000Z", total: 110, input: 100, cached: 40, output: 10),
            UsageFixture.CodexTokens("2026-10-17T08:00:01.500Z", total: 110, input: 100, cached: 40, output: 10),   // the same turn reported again
            UsageFixture.CodexTokens("2026-10-17T08:05:00.000Z", total: 200, input: 80, cached: 0, output: 10));

        var used = await new CodexUsage(_files.Root).Between(_from, _to, Token);

        Expect.Equal(new ModelTokens("gpt-5", Input: 60 + 80, CacheRead: 40, CacheWrite: 0, Output: 20), Assert.Single(used));
    }

    [Fact]
    public async Task A_session_is_read_whole_by_its_id()
    {
        _files.Write($"2026/01/01/rollout-2026-01-01T08-00-00-{Id}.jsonl",
            UsageFixture.CodexModel("2026-01-01T08:00:00.000Z", "gpt-5"),
            UsageFixture.CodexTokens("2026-01-01T08:00:01.000Z", total: 11, input: 10, cached: 0, output: 1));
        _files.Write("2026/10/17/rollout-2026-10-17T08-00-00-0198f2a1-0000-7000-8000-000000000002.jsonl",
            UsageFixture.CodexTokens("2026-10-17T08:00:01.000Z", total: 99, input: 90, cached: 0, output: 9));

        var used = await new CodexUsage(_files.Root).OfSession(SessionId.From(Id), Token);

        Expect.Equal(new ModelTokens("gpt-5", 10, 0, 0, 1), Assert.Single(used));
    }
}
