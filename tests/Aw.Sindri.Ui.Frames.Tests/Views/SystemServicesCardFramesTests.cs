using System.Globalization;
using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The System page's Services card keeps a service name and its cadence apart at 1280 (#3747): a long name fits its column or is cut with an ellipsis,
/// and a gap separates it from the cadence text.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class SystemServicesCardFramesTests : FrameHost
{
    private const string Services = """{"services":[{"name":"code-index-main-move","every":"1m","paused":false},{"name":"context-concept-scan","every":"1m","paused":false},{"name":"heartbeat","every":"5s","paused":false}],"empty":null,"asOf":{"cursor":41,"ledgerHead":41,"projectedAt":"2026-01-01T00:00:58.0000000Z","rebuilding":false,"stalled":false}}""";

    [Fact]
    public Task At_1280_a_service_name_ends_before_its_cadence() => OnUi(async () =>
    {
        Server.ServiceView = new RouteAnswer(HttpStatusCode.OK, Services);
        var (window, shell) = await Connected(width: 1280);
        shell.GoTo(PageId.System);
        await Until(window, () => Named<ItemsControl>(window, "ServicesList").GetVisualDescendants().OfType<Grid>().Count(g => g.Classes.Contains("data-row")) >= 3, "the Services card never showed its rows");
        Settle(window);

        var rows = Named<ItemsControl>(window, "ServicesList").GetVisualDescendants().OfType<Grid>().Where(g => g.Classes.Contains("data-row")).ToList();
        foreach (var row in rows)
        {
            var name = row.Children.OfType<TextBlock>().First();
            var every = row.Children.OfType<TextBlock>().ElementAt(1);
            var nameRight = name.TranslatePoint(new Point(name.Bounds.Width, 0), window)!.Value.X;
            var everyLeft = every.TranslatePoint(new Point(0, 0), window)!.Value.X;

            var probe = new TextBlock { Text = name.Text, FontSize = name.FontSize, FontWeight = name.FontWeight, FontFamily = name.FontFamily };
            probe.Measure(Size.Infinity);
            var fits = probe.DesiredSize.Width <= name.Bounds.Width + 0.5;

            Assert.True(fits || name.TextTrimming == TextTrimming.CharacterEllipsis, string.Create(CultureInfo.InvariantCulture, $"'{name.Text}' needs {probe.DesiredSize.Width} px, its column gives {name.Bounds.Width}, and it does not truncate"));
            Assert.True(nameRight + 4 <= everyLeft, string.Create(CultureInfo.InvariantCulture, $"'{name.Text}' ends at {nameRight}, the cadence starts at {everyLeft}: no gap"));
        }
    });
}
