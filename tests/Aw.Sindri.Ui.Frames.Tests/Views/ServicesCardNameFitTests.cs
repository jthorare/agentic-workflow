using System.Globalization;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The System page's Services card shows each built-in service name whole, clear of its cadence (#3747).</summary>
[Lane(TestLane.Integration)]
public sealed class ServicesCardNameFitTests : FrameHost
{
    private const double MinGap = 8;

    [Theory]
    [InlineData(1000)]
    [InlineData(1280)]
    public Task The_longest_service_name_is_not_clipped_and_does_not_touch_its_cadence(double width) => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: width);
        shell.GoTo(PageId.System);
        Settle(window);

        var list = Named<ItemsControl>(window, "ServicesList");
        await Until(window, () => NameBlocks(list).Count > 0, "the Services card never listed a service");

        var names = NameBlocks(list);
        var longest = names.MaxBy(n => n.Text!.Length)!;
        var row = Assert.IsType<Grid>(longest.Parent);
        var cadence = Assert.IsType<TextBlock>(row.Children[1]);
        var culture = CultureInfo.InvariantCulture;

        var column = row.ColumnDefinitions[0].ActualWidth;
        var textWidth = longest.TextLayout.Width;
        Assert.True(textWidth <= column, string.Create(culture, $"'{longest.Text}' needs {textWidth} px, its column is {column} px wide and clips it"));

        var nameRight = longest.TranslatePoint(default, window)!.Value.X + textWidth;
        var cadenceLeft = cadence.TranslatePoint(default, window)!.Value.X;
        Assert.True(cadenceLeft - nameRight >= MinGap, string.Create(culture, $"'{longest.Text}' ends at {nameRight}, '{cadence.Text}' starts at {cadenceLeft}: under the {MinGap} px gap"));
    });

    private static List<TextBlock> NameBlocks(ItemsControl list) =>
        [.. list.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("body-strong") && t.Parent is Grid { Children.Count: 3 } && !string.IsNullOrEmpty(t.Text))];
}
