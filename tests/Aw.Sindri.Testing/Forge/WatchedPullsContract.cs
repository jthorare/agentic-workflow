using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Testing.Forge;

/// <summary>What every <see cref="IWatchedPulls"/> keeps to: the pull requests instances wait on, once each, in a stable order.</summary>
public abstract class WatchedPullsContract
{
    private const string One = "https://forge.test/acme/widgets/pull/1";
    private const string Two = "https://forge.test/acme/widgets/pull/2";

    /// <summary>Creates an empty source.</summary>
    /// <returns>The port and the hands that move its instances.</returns>
    protected abstract WatchedPullsUnderTest Create();

    [Fact]
    public async Task Nothing_waiting_lists_nothing()
    {
        var source = Create();

        var listed = await source.Pulls.List(TestContext.Current.CancellationToken);

        Assert.Empty(listed.Value);
    }

    [Fact]
    public async Task A_waiting_instance_lists_its_link_and_item_once()
    {
        var source = Create();
        source.Watch(One, "ITM-2");
        source.Watch(One, "ITM-2");
        source.Watch(Two, "ITM-1");

        var listed = await source.Pulls.List(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { new WatchedPull(Two, "ITM-1"), new WatchedPull(One, "ITM-2") }, listed.Value);
    }

    [Fact]
    public async Task An_instance_that_stops_waiting_is_no_longer_listed()
    {
        var source = Create();
        source.Watch(One, "ITM-1");
        source.Watch(Two, "ITM-2");
        source.Unwatch(One);

        var listed = await source.Pulls.List(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { new WatchedPull(Two, "ITM-2") }, listed.Value);
    }
}
