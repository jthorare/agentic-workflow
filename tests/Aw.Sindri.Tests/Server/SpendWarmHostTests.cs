using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Server;

/// <summary>#3213: serve reads each agent's spend for the day soon after it starts, so the first request after start does not wait on the transcript folders.</summary>
[Lane(TestLane.Unit)]
public sealed class SpendWarmHostTests
{
    private static readonly AgentId _claude = AgentId.From("claude-code");
    private static readonly AgentId _codex = AgentId.From("codex");
    private static readonly AgentId _plain = AgentId.From("plain");

    private sealed class RecordingSpend : ILaunchSpend
    {
        private readonly Lock _gate = new();
        private readonly List<AgentId> _asked = [];

        public IReadOnlyList<AgentId> Asked
        {
            get
            {
                lock (_gate)
                {
                    return [.. _asked];
                }
            }
        }

        public TaskCompletionSource<bool> EachTwice { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> Two { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SpendPeek Peek(AgentId target) => new(Landed: true, Spend: null);

        public ValueTask<Result<AgentSpend?>> Today(AgentId target, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _asked.Add(target);
                if (_asked.Where(static a => a == _claude).Skip(1).Any() && _asked.Where(static a => a == _codex).Skip(1).Any())
                {
                    _ = EachTwice.TrySetResult(true);
                }

                if (_asked.Count >= 2)
                {
                    _ = Two.TrySetResult(true);
                }
            }

            return ValueTask.FromResult(Results.Success(default(AgentSpend)));
        }
    }

    /// <summary>A catalog whose first read of <see cref="All"/> throws and later ones answer.</summary>
    private sealed class ThrowsOnceCatalog(params AgentEntry[] entries) : IAgentCatalog
    {
        private int _reads;

        public TaskCompletionSource<bool> Threw { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<AgentEntry> All
        {
            get
            {
                if (Interlocked.Increment(ref _reads) == 1)
                {
                    _ = Threw.TrySetResult(true);
                    throw new InvalidOperationException("the catalog could not be read");
                }

                return entries;
            }
        }

        public AgentEntry? Resolve(AgentId id) => entries.FirstOrDefault(e => e.Id == id);
    }

    private static AgentEntry Entry(AgentId id, string? usage) => new(id, id.Value, id.Value, ProcessName: null, Transcripts: null, Resume: null) { Usage = usage };

    [Fact]
    public async Task The_spend_warm_host_reads_every_agent_with_a_reader_soon_after_start()
    {
        var spend = new RecordingSpend();
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var catalog = new FixedAgentCatalog(Entry(_claude, "claude-code"), Entry(_codex, "codex"), Entry(_plain, "none"));
        using var host = new SpendWarmHost(catalog, spend, clock, NullLogger<SpendWarmHost>.Instance);
        var token = TestContext.Current.CancellationToken;

        await host.StartAsync(token);
        clock.Advance(TimeSpan.FromSeconds(5));
        _ = await spend.Two.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, token);
        await host.StopAsync(token);

        Assert.Equivalent(new[] { _claude, _codex }, spend.Asked.Distinct().ToArray(), strict: false);
        Assert.DoesNotContain(_plain, spend.Asked);
    }

    [Fact]
    public async Task The_spend_warm_host_reads_again_every_30_seconds()
    {
        var spend = new RecordingSpend();
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var catalog = new FixedAgentCatalog(Entry(_claude, "claude-code"), Entry(_codex, "codex"), Entry(_plain, "none"));
        using var host = new SpendWarmHost(catalog, spend, clock, NullLogger<SpendWarmHost>.Instance);
        var token = TestContext.Current.CancellationToken;

        await host.StartAsync(token);
        clock.Advance(TimeSpan.FromSeconds(5));
        _ = await spend.Two.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, token);

        // the loop creates its 30 s wait after the first reads finish, so advance in steps until the second pass has run
        for (var i = 0; i < 500 && !spend.EachTwice.Task.IsCompleted; i++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), TimeProvider.System, token);
            clock.Advance(TimeSpan.FromSeconds(30));
        }

        _ = await spend.EachTwice.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, token);
        await host.StopAsync(token);
        Assert.DoesNotContain(_plain, spend.Asked);
    }

    /// <summary>Pins #3407: a catalog read that throws is logged and the next pass warms; it never faults the host, which would stop serve.</summary>
    [Fact]
    public async Task A_catalog_read_that_throws_once_is_logged_and_the_next_pass_warms()
    {
        var spend = new RecordingSpend();
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var catalog = new ThrowsOnceCatalog(Entry(_claude, "claude-code"), Entry(_codex, "codex"));
        using var host = new SpendWarmHost(catalog, spend, clock, NullLogger<SpendWarmHost>.Instance);
        var token = TestContext.Current.CancellationToken;

        await host.StartAsync(token);
        clock.Advance(TimeSpan.FromSeconds(5));
        _ = await catalog.Threw.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, token);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TimeProvider.System, token);   // the throw has had time to fault the loop if nothing catches it

        Assert.False(host.ExecuteTask!.IsFaulted, "the host's loop faulted on the catalog throw");

        // the loop creates its 30 s wait after the first pass, so advance in steps until the next pass has asked both agents
        for (var i = 0; i < 500 && !spend.Two.Task.IsCompleted; i++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), TimeProvider.System, token);
            clock.Advance(TimeSpan.FromSeconds(30));
        }

        _ = await spend.Two.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, token);
        await host.StopAsync(token);
        Assert.Equivalent(new[] { _claude, _codex }, spend.Asked.Distinct().ToArray(), strict: false);
    }
}
