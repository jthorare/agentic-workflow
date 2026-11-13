using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
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

    /// <summary>Pins #2777: a stale day answers from the last read and starts one refresh in the background, so a caller never waits on the transcripts.</summary>
    [Fact]
    public async Task A_stale_day_answers_from_the_last_read_and_never_waits_on_the_next()
    {
        var gate = new TaskCompletionSource<IReadOnlyList<ModelTokens>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new GatedUsageReader([new ModelTokens("claude-sonnet-4-5", 1_000_000, 0, 0, 0)], gate.Task);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 17, 20, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("test", _utcMinusSeven, "test", "test"));
        var spend = new UsageSpend(new AgentCatalog([AgentEntries.ClaudeCode]), new Dictionary<string, IUsageReader>(StringComparer.Ordinal) { ["claude-code"] = reader }, clock);
        var first = new AgentSpend(3m, 1_000_000, SpendBasis.PriceTable);
        var second = new AgentSpend(6m, 2_000_000, SpendBasis.PriceTable);

        Expect.Equal(first, (await spend.Today(_claude, Token)).Value);
        clock.Advance(TimeSpan.FromSeconds(16));

        var one = await spend.Today(_claude, Token).AsTask().WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, Token);
        var two = await spend.Today(_claude, Token).AsTask().WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, Token);

        Expect.Equal(first, one.Value);
        Expect.Equal(first, two.Value);
        Expect.Equal(2, reader.Reads);   // the first read and one shared refresh, not one per caller

        gate.SetResult([new ModelTokens("claude-sonnet-4-5", 2_000_000, 0, 0, 0)]);
        var answer = (await spend.Today(_claude, Token)).Value;
        for (var waited = 0; waited < 200 && answer != second; waited++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), TimeProvider.System, Token);
            answer = (await spend.Today(_claude, Token)).Value;
        }

        Expect.Equal(second, answer);
    }

    /// <summary>#3213: a caller that asks while the day's first read runs shares that read; nothing starts a second one.</summary>
    [Fact]
    public async Task A_read_asked_while_the_first_read_of_the_day_runs_waits_for_that_read()
    {
        var gate = new TaskCompletionSource<IReadOnlyList<ModelTokens>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new FirstReadGatedReader(gate.Task);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 17, 20, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("test", _utcMinusSeven, "test", "test"));
        var spend = new UsageSpend(new AgentCatalog([AgentEntries.ClaudeCode]), new Dictionary<string, IUsageReader>(StringComparer.Ordinal) { ["claude-code"] = reader }, clock);

        var one = spend.Today(_claude, Token).AsTask();
        var two = spend.Today(_claude, Token).AsTask();
        _ = await reader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);
        Expect.Equal(1, reader.Reads);
        Assert.False(one.IsCompleted || two.IsCompleted);

        gate.SetResult([new ModelTokens("claude-sonnet-4-5", 1_000_000, 0, 0, 0)]);
        var both = await Task.WhenAll(one, two).WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);

        var expected = new AgentSpend(3m, 1_000_000, SpendBasis.PriceTable);
        Expect.Equal(expected, both[0].Value);
        Expect.Equal(expected, both[1].Value);
        Expect.Equal(1, reader.Reads);
    }

    /// <summary>#3213 through #3418: every agent's first walk starts at once, so the slowest agent sets the wait, not the sum.</summary>
    [Fact]
    public async Task Peeks_of_several_agents_start_their_reads_together()
    {
        var claudeGate = new TaskCompletionSource<IReadOnlyList<ModelTokens>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var codexGate = new TaskCompletionSource<IReadOnlyList<ModelTokens>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var claude = new FirstReadGatedReader(claudeGate.Task);
        var codex = new FirstReadGatedReader(codexGate.Task);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 17, 20, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("test", _utcMinusSeven, "test", "test"));
        var readers = new Dictionary<string, IUsageReader>(StringComparer.Ordinal) { ["claude-code"] = claude, ["codex"] = codex };
        var spend = new UsageSpend(new AgentCatalog([AgentEntries.ClaudeCode, AgentEntries.Codex]), readers, clock);

        var first = spend.Peek(_claude);
        var second = spend.Peek(AgentId.From("codex"));
        try
        {
            _ = await Task.WhenAll(claude.Started.Task, codex.Started.Task).WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);

            Assert.False(first.Landed);
            Assert.False(second.Landed);
            Expect.Equal((1, 1), (claude.Reads, codex.Reads));
        }
        finally
        {
            _ = claudeGate.TrySetResult([]);
            _ = codexGate.TrySetResult([]);
        }
    }

    /// <summary>#3418: a peek never waits; the first one starts the shared read and says it hasn't landed, a later one returns the spend.</summary>
    [Fact]
    public async Task A_peek_on_a_cold_reader_starts_one_read_and_returns_the_spend_once_it_landed()
    {
        var gate = new TaskCompletionSource<IReadOnlyList<ModelTokens>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new FirstReadGatedReader(gate.Task);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 17, 20, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("test", _utcMinusSeven, "test", "test"));
        var spend = new UsageSpend(new AgentCatalog([AgentEntries.ClaudeCode]), new Dictionary<string, IUsageReader>(StringComparer.Ordinal) { ["claude-code"] = reader }, clock);

        var cold = spend.Peek(_claude);
        _ = await reader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);
        var again = spend.Peek(_claude);

        Assert.False(cold.Landed);
        Assert.False(again.Landed);
        Expect.Equal(1, reader.Reads);

        gate.SetResult([new ModelTokens("claude-sonnet-4-5", 1_000_000, 0, 0, 0)]);
        var landed = spend.Peek(_claude);
        for (var waited = 0; waited < 500 && !landed.Landed; waited++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), TimeProvider.System, Token);
            landed = spend.Peek(_claude);
        }

        Assert.True(landed.Landed);
        Expect.Equal(new AgentSpend(3m, 1_000_000, SpendBasis.PriceTable), landed.Spend);
        Expect.Equal(1, reader.Reads);
    }

    /// <summary>#3213: the cached read belongs to one local day; after midnight the next call reads the new day's window.</summary>
    [Fact]
    public async Task A_new_local_day_reads_again()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 17, 20, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("test", _utcMinusSeven, "test", "test"));
        var reader = new FakeUsageReader("claude-code", verified: true) { Used = [new ModelTokens("claude-sonnet-4-5", 1_000_000, 0, 0, 0)] };
        var spend = new UsageSpend(new AgentCatalog([AgentEntries.ClaudeCode]), new Dictionary<string, IUsageReader>(StringComparer.Ordinal) { ["claude-code"] = reader }, clock);
        Expect.Equal(new AgentSpend(3m, 1_000_000, SpendBasis.PriceTable), (await spend.Today(_claude, Token)).Value);

        clock.Advance(TimeSpan.FromHours(12));   // 01:00 on 18 October in UTC-7
        reader.Used = [new ModelTokens("claude-sonnet-4-5", 2_000_000, 0, 0, 0)];
        var today = (await spend.Today(_claude, Token)).Value;

        Expect.Equal(new AgentSpend(6m, 2_000_000, SpendBasis.PriceTable), today);
        Expect.Equal(2, reader.Reads);
        Expect.Equal((new DateTimeOffset(2026, 10, 18, 7, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 19, 7, 0, 0, TimeSpan.Zero)), reader.Asked);
    }

    /// <summary>A reader whose every read waits on the gate, and says when the first one began.</summary>
    private sealed class FirstReadGatedReader(Task<IReadOnlyList<ModelTokens>> gate) : IUsageReader
    {
        private int _reads;

        public int Reads => Volatile.Read(ref _reads);

        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name => "claude-code";

        public bool Verified => true;

        public async ValueTask<IReadOnlyList<ModelTokens>> Between(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _reads);
            _ = Started.TrySetResult(true);
            return await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public ValueTask<IReadOnlyList<ModelTokens>> OfSession(SessionId session, CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<ModelTokens>>([]);
    }

    /// <summary>A reader that answers its first read at once and every later one only when the gate opens.</summary>
    private sealed class GatedUsageReader(IReadOnlyList<ModelTokens> firstRead, Task<IReadOnlyList<ModelTokens>> later) : IUsageReader
    {
        private int _reads;

        public int Reads => Volatile.Read(ref _reads);

        public string Name => "claude-code";

        public bool Verified => true;

        public async ValueTask<IReadOnlyList<ModelTokens>> Between(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
            Interlocked.Increment(ref _reads) == 1 ? firstRead : await later.WaitAsync(cancellationToken).ConfigureAwait(false);

        public ValueTask<IReadOnlyList<ModelTokens>> OfSession(SessionId session, CancellationToken cancellationToken) => ValueTask.FromResult(firstRead);
    }
}
