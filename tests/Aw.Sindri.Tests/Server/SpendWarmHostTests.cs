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

        public TaskCompletionSource<bool> Two { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<Result<AgentSpend?>> Today(AgentId target, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _asked.Add(target);
                if (_asked.Count >= 2)
                {
                    _ = Two.TrySetResult(true);
                }
            }

            return ValueTask.FromResult(Results.Success(default(AgentSpend)));
        }
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
}
