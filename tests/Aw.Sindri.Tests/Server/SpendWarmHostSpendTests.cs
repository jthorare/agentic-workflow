using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Launch.Usage;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Server;

/// <summary>#3213: the warm host and the real <see cref="UsageSpend"/> together: a request after the warm-up gets the warm read, and never starts a second one.</summary>
[Lane(TestLane.Unit)]
public sealed class SpendWarmHostSpendTests
{
    private static readonly AgentId _claude = AgentId.From("claude-code");
    private static readonly IReadOnlyList<ModelTokens> _used = [new ModelTokens("claude-sonnet-4-5", 1_000_000, 0, 0, 0)];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class CountingReader(Task<IReadOnlyList<ModelTokens>> gate) : IUsageReader
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

        public ValueTask<IReadOnlyList<ModelTokens>> OfSession(SessionId session, CancellationToken cancellationToken) => ValueTask.FromResult(_used);
    }

    private static (SpendWarmHost Host, UsageSpend Spend, FakeTimeProvider Clock) Make(CountingReader reader)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 17, 20, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(-7), "test", "test"));
        var catalog = new AgentCatalog([AgentEntries.ClaudeCode]);
        var spend = new UsageSpend(catalog, new Dictionary<string, IUsageReader>(StringComparer.Ordinal) { ["claude-code"] = reader }, clock);
        return (new SpendWarmHost(catalog, spend, clock, NullLogger<SpendWarmHost>.Instance), spend, clock);
    }

    [Fact]
    public async Task A_spend_read_after_the_warm_up_uses_the_warm_read()
    {
        var reader = new CountingReader(Task.FromResult(_used));
        var (host, spend, clock) = Make(reader);
        using var warm = host;

        await host.StartAsync(Token);
        clock.Advance(TimeSpan.FromSeconds(5));
        _ = await reader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);

        var today = (await spend.Today(_claude, Token).AsTask().WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token)).Value;

        Expect.Equal(new AgentSpend(3m, 1_000_000, SpendBasis.PriceTable), today);
        Expect.Equal(1, reader.Reads);
        await host.StopAsync(Token);
    }

    [Fact]
    public async Task A_spend_read_while_the_warm_read_runs_waits_for_it()
    {
        var gate = new TaskCompletionSource<IReadOnlyList<ModelTokens>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new CountingReader(gate.Task);
        var (host, spend, clock) = Make(reader);
        using var warm = host;

        await host.StartAsync(Token);
        clock.Advance(TimeSpan.FromSeconds(5));
        _ = await reader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);

        var asked = spend.Today(_claude, Token).AsTask();
        Assert.False(asked.IsCompleted);
        gate.SetResult(_used);
        var today = (await asked.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token)).Value;

        Expect.Equal(new AgentSpend(3m, 1_000_000, SpendBasis.PriceTable), today);
        Expect.Equal(1, reader.Reads);
        await host.StopAsync(Token);
    }
}
