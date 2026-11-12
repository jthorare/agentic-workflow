using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.AgoTicks;

/// <summary>#3347: a service tile's "Last used 5m ago" is announced as changed (its getter alone does not reach the screen).</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectionTileAgoTickTests
{
    [Fact]
    public Task The_tile_says_its_last_used_words_changed_when_the_clock_moves() => UiThread.Run(async () =>
    {
        var host = new AgoTickHost(AgoTickHost.GoldenDay);
        _ = host.Server.SeedConnection(FakeSindriApi.ConnectionRow("linear:work") with { LastRefreshedAt = host.Clock.GetUtcNow() - TimeSpan.FromMinutes(5) });
        var shell = await host.Start();
        shell.GoTo(PageId.Connections);
        await AgoTickHost.Until(() => shell.Connections.Tiles.Any(t => string.Equals(t.Id, "linear", StringComparison.Ordinal) && t.LastUsedText.StartsWith("Last used", StringComparison.Ordinal)));
        var tile = shell.Connections.Tiles.Single(t => string.Equals(t.Id, "linear", StringComparison.Ordinal));
        var before = tile.LastUsedText;
        var announced = new List<string?>();
        tile.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

        host.Clock.Advance(TimeSpan.FromMinutes(2));

        await AgoTickHost.Until(() => announced.Contains(nameof(tile.LastUsedText), StringComparer.Ordinal));
        Assert.NotEqual(before, tile.LastUsedText, StringComparer.Ordinal);
    }, AgoTickHost.Token);
}
