using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>Closing an agent Step (engine P8 D9): a final usage sample is taken, then the tokens are read, then the session is stopped.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentClosingTests
{
    private static readonly LaunchId _launch = A.FirstLaunchId;
    private readonly List<string> _order = [];
    private readonly Flushes _sampler;
    private readonly ReportRefusals _refusals = new();

    public AgentClosingTests()
    {
        _sampler = new Flushes(_order);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private AgentClosing Closing(Sessions? sessions = null)
    {
        var services = new ServiceCollection()
            .AddSingleton<UsageFlush>((launch, ct) => new ValueTask(_sampler.Flush(launch, ct).AsTask()))
            .AddSingleton<IAgentSessions>(sessions ?? new Sessions(_order))
            .AddSingleton<IRunDiff>(new FakeRunDiff())
            .AddSingleton<ILauncher>(new Stops(_order))
            .AddSingleton(_refusals)
            .AddSingleton<ILogger<AgentClosing>>(NullLogger<AgentClosing>.Instance)
            .AddSingleton<AgentClosing>();
        return services.BuildServiceProvider().GetRequiredService<AgentClosing>();
    }

    [Fact]
    public async Task A_close_flushes_then_measures_then_stops()
    {
        var closed = await Closing().Close(new AgentCloseRun(_launch.Value, Worktree: null, StartHead: null, Measure: true), Token);

        Assert.Equal(["flush", "usage", "stop"], _order);
        Assert.Equal(42, closed.Tokens);
    }

    [Fact]
    public async Task A_close_forgets_the_launchs_refusal_so_nothing_leaks_when_the_watch_never_asked()
    {
        _refusals.Record(_launch, "fact:design isn't JSON");

        _ = await Closing().Close(new AgentCloseRun(_launch.Value, Worktree: null, StartHead: null, Measure: false), Token);

        Assert.Null(_refusals.Take(_launch));
    }

    [Fact]
    public async Task A_close_that_does_not_measure_takes_no_sample()
    {
        _ = await Closing().Close(new AgentCloseRun(_launch.Value, Worktree: null, StartHead: null, Measure: false), Token);

        Assert.Equal(["stop"], _order);
    }

    [Fact]
    public async Task A_flush_that_fails_does_not_fail_the_close_and_the_last_sample_stands()
    {
        _sampler.Throws = true;

        var closed = await Closing().Close(new AgentCloseRun(_launch.Value, Worktree: null, StartHead: null, Measure: true), Token);

        Assert.Equal(["flush", "usage", "stop"], _order);
        Assert.Equal(42, closed.Tokens);
    }

    private sealed class Flushes(List<string> order) : ISessionUsageSampler
    {
        public bool Throws { get; set; }

        public ValueTask<int> Sample(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<int> Flush(LaunchId launch, CancellationToken cancellationToken)
        {
            order.Add("flush");
            return Throws ? throw new InvalidOperationException("the ledger is busy") : ValueTask.FromResult(1);
        }
    }

    private sealed class Sessions(List<string> order) : IAgentSessions
    {
        public ValueTask<Result<AgentSessionState>> Watch(LaunchId launch, CancellationToken cancellationToken) => ValueTask.FromResult<Result<AgentSessionState>>(AgentSessionState.Live);

        public ValueTask<Result<long>> Usage(LaunchId launch, CancellationToken cancellationToken)
        {
            order.Add("usage");
            return ValueTask.FromResult<Result<long>>(42L);
        }
    }

    private sealed class Stops(List<string> order) : ILauncher
    {
        public ValueTask<LaunchResult> Launch(LaunchRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<Unit>> Stop(LaunchId launch, CancellationToken cancellationToken)
        {
            order.Add("stop");
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }

        public ValueTask<LaunchProbed> Probe(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
