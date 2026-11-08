using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// The contract every <see cref="ILaunchRequests"/> honours (UI spec E-19): an item with no launch in flight reads none,
/// so a start runs as it always did. Until API P10a's launches projection lands the adapter reads none for every item.
/// </summary>
public abstract class LaunchRequestsContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The launches.</summary>
    /// <returns>The port under test, with no launch requested.</returns>
    protected abstract ILaunchRequests Create();

    /// <summary>The port under test with <paramref name="launch"/> requested for <paramref name="item"/>.</summary>
    /// <param name="item">The item.</param>
    /// <param name="launch">The launch id.</param>
    /// <returns>The port, or null when the adapter has no launches to request (the stub until API P10a).</returns>
    protected abstract ILaunchRequests? CreateRequesting(ItemKey item, LaunchId launch);

    /// <summary>A requested launch reads as its id for its item, and only for that item.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_requested_launch_reads_as_its_id_for_its_item_only()
    {
        var launches = CreateRequesting(ItemKey.From("T-2"), LaunchId.From("LCH-0123456789abcdef0123456789abcdef"));
        if (launches is null)
        {
            return;
        }

        var own = await launches.Requested(ItemKey.From("T-2"), Token);
        var other = await launches.Requested(ItemKey.From("T-3"), Token);

        Expect.True(!own.IsError && own.Value is { } open
            && string.Equals(open.Value, "LCH-0123456789abcdef0123456789abcdef", StringComparison.Ordinal));
        Expect.True(!other.IsError && other.Value is null);
    }

    /// <summary>An item with no launch in flight reads none.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_item_with_no_launch_in_flight_reads_none()
    {
        var read = await Create().Requested(ItemKey.From("T-1"), Token);

        Expect.True(!read.IsError);
        Expect.True(read.Value is null);
    }
}
