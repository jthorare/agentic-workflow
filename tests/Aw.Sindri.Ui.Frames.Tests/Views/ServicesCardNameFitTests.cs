using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Theme;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The System page's Services card shows each built-in service name whole, clear of its cadence (#3747).</summary>
[Lane(TestLane.Integration)]
public sealed class ServicesCardNameFitTests : FrameHost
{
    private const double MinGap = 8;

    [Theory]
    [InlineData(1000, 1.0)]
    [InlineData(1000, 1.3)]
    [InlineData(1280, 1.0)]
    [InlineData(1280, 1.3)]
    public Task The_widest_service_name_is_not_clipped_and_does_not_touch_its_cadence(double width, double scale) => OnUi(async () =>
    {
        // The default fixture lists only "heartbeat"; serve the built-in names (longest: 20 characters) so the widest name is on screen.
        Server.ServiceView = RouteAnswer.Api("services", "ran-ok");
        var (window, shell) = await Connected(width: width);
        var resources = Application.Current!.Resources;
        TextScale.Apply(resources, scale);
        try
        {
            await Check(window, shell);
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
        }
    });

    private static async Task Check(ShellWindow window, ShellViewModel shell)
    {
        shell.GoTo(PageId.System);
        Settle(window);

        var list = Named<ItemsControl>(window, "ServicesList");
        await Until(window, () => NameBlocks(list).Count > 0, "the Services card never listed a service");

        var names = NameBlocks(list);
        var longest = names.MaxBy(n => n.TextLayout.Width)!;
        var row = Assert.IsType<Grid>(longest.Parent);
        var cadence = Assert.IsType<TextBlock>(row.Children[1]);
        var culture = CultureInfo.InvariantCulture;

        var column = row.ColumnDefinitions[0].ActualWidth;
        var textWidth = longest.TextLayout.Width;
        Assert.True(textWidth <= column, string.Create(culture, $"'{longest.Text}' needs {textWidth} px, its column is {column} px wide and clips it"));

        var nameRight = longest.TranslatePoint(default, window)!.Value.X + textWidth;
        var cadenceLeft = cadence.TranslatePoint(default, window)!.Value.X;
        Assert.True(cadenceLeft - nameRight >= MinGap, string.Create(culture, $"'{longest.Text}' ends at {nameRight}, '{cadence.Text}' starts at {cadenceLeft}: under the {MinGap} px gap"));
    }

    private static List<TextBlock> NameBlocks(ItemsControl list) =>
        [.. list.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("body-strong") && t.Parent is Grid { Children.Count: 3 } && !string.IsNullOrEmpty(t.Text))];
}
