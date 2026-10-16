using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// The contract every <see cref="IInstanceLiveness"/> honours (engine spec §5.1, §12; engine P6 unit (d)): an instance
/// that was never started is not live, one that runs is, and one that ended is not any more. 'claimExists' and the
/// '--attempt next' check (SND-WF-012) judge a claim's holder by exactly this.
/// </summary>
public abstract class InstanceLivenessContract
{
    private static int _next = 900;

    /// <summary>The address this test starts and ends; each test has its own, so a shared server never sees one twice.</summary>
    protected InstanceAddress Address { get; } = InstanceAddress.From(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"T-{Interlocked.Increment(ref _next)}/step/1"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The liveness under test, with no instance started.</summary>
    /// <returns>The port.</returns>
    protected abstract ValueTask<IInstanceLiveness> Create();

    /// <summary>Starts <see cref="Address"/>: it runs from now on.</summary>
    /// <returns>Done once it runs.</returns>
    protected abstract Task Run();

    /// <summary>Ends <see cref="Address"/>.</summary>
    /// <returns>Done once it has ended.</returns>
    protected abstract Task End();

    /// <summary>An address nothing started is not live, and that is an answer, not an error.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_instance_never_started_is_not_live()
    {
        var liveness = await Create();

        var live = await liveness.IsLive(Address, Token);

        Expect.True(live.IsSuccess, live.IsError ? live.Error.ToString() : string.Empty);
        Expect.True(!live.Value);
    }

    /// <summary>A started instance is live while it runs.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_running_instance_is_live()
    {
        var liveness = await Create();
        await Run();

        var live = await liveness.IsLive(Address, Token);

        Expect.True(live.IsSuccess);
        Expect.True(live.Value);
    }

    /// <summary>An instance that ended is no longer live.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_ended_instance_is_not_live()
    {
        var liveness = await Create();
        await Run();
        await End();

        var live = await liveness.IsLive(Address, Token);

        Expect.True(live.IsSuccess);
        Expect.True(!live.Value);
    }
}
