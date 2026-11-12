using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.AgoTicks;

/// <summary>#3347: the item sheet's status line ("New for 5m") and age move with the clock on the one-second beat.</summary>
[Lane(TestLane.Unit)]
public sealed class ItemSheetAgoTickTests
{
    [Fact]
    public Task The_status_line_and_age_move_with_the_clock() => UiThread.Run(async () =>
    {
        var host = new AgoTickHost(A.Noon);
        var shell = await host.Start();
        shell.Navigate(new DeepLink(DeepLinkKind.Item, A.NewItem));
        await AgoTickHost.Until(() => shell.Item.HasItem && shell.Item.StatusLine.Length > 0);
        Assert.Equal(("New for 5m", "5m"), (shell.Item.StatusLine, shell.Item.Age));

        host.Clock.Advance(TimeSpan.FromMinutes(2));

        await AgoTickHost.Until(() => (shell.Item.StatusLine, shell.Item.Age) == ("New for 7m", "7m"));
    }, AgoTickHost.Token);
}
