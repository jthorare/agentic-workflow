using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Launch.Usage;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch.Usage;

/// <summary>An agent's spend today (screens spec E-28): the entry's usage reader over the local day, priced by the table.</summary>
[Lane(TestLane.Unit)]
public sealed class UsageSpendTests
{
    private static readonly AgentId _claude = AgentId.From("claude-code");
    private static readonly TimeSpan _utcMinusSeven = TimeSpan.FromHours(-7);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (UsageSpend Spend, FakeUsageReader Reader) Make(params AgentEntry[] entries) => Make(verified: true, entries);

    private static (UsageSpend Spend, FakeUsageReader Reader) Make(bool verified, params AgentEntry[] entries)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 17, 20, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("test", _utcMinusSeven, "test", "test"));
        var reader = new FakeUsageReader("claude-code", verified);
        return (new UsageSpend(new AgentCatalog(entries.Length == 0 ? [AgentEntries.ClaudeCode] : entries), new Dictionary<string, IUsageReader>(StringComparer.Ordinal) { ["claude-code"] = reader }, clock), reader);
    }

    [Fact]
    public async Task Spend_is_local_day_priced()
    {
        var (spend, reader) = Make();
        reader.Used = [new ModelTokens("claude-sonnet-4-5", Input: 1_000_000, CacheRead: 0, CacheWrite: 0, Output: 1_000_000)];

        var today = (await spend.Today(_claude, Token)).Value;

        Expect.Equal(new AgentSpend(18m, 2_000_000, SpendBasis.PriceTable), today);
        Expect.Equal((new DateTimeOffset(2026, 10, 17, 7, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 18, 7, 0, 0, TimeSpan.Zero)), reader.Asked);   // 17 October in UTC-7
    }

    [Fact]
    public async Task Tokens_only_when_model_unpriced()
    {
        var (spend, reader) = Make();
        reader.Used = [new ModelTokens("mystery-9", 10, 0, 0, 10)];

        var today = (await spend.Today(_claude, Token)).Value;

        Expect.Equal(new AgentSpend(0m, 20, SpendBasis.TokensOnly), today);
    }

    [Fact]
    public async Task An_unverified_reader_counts_tokens_only()
    {
        var (spend, reader) = Make(verified: false);
        reader.Used = [new ModelTokens("claude-sonnet-4-5", 1_000_000, 0, 0, 0)];

        var today = (await spend.Today(_claude, Token)).Value;

        Expect.Equal(new AgentSpend(0m, 1_000_000, SpendBasis.TokensOnly), today);
    }

    [Fact]
    public async Task An_entry_with_usage_none_or_an_unknown_agent_has_no_reader()
    {
        var (spend, _) = Make(AgentEntries.ClaudeCode, AgentEntries.Aider);

        Assert.Null((await spend.Today(AgentId.From("aider"), Token)).Value);
        Assert.Null((await spend.Today(AgentId.From("ghost"), Token)).Value);
    }

    [Fact]
    public async Task The_day_is_read_once_a_few_seconds()
    {
        var (spend, reader) = Make();

        _ = await spend.Today(_claude, Token);
        _ = await spend.Today(_claude, Token);

        Expect.Equal(1, reader.Reads);
    }
}
