using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// #3212: Home's "Nothing needs you." line starts on the card's text column (24), left-aligned, like the count line and the failed caption.
/// It used .quiet, which centres the text, instead of .subtle.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouEmptyLayoutTests : FrameHost
{
    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task The_empty_line_starts_on_the_cards_24_column(double width) => OnUi(async () =>
    {
        Server.NeedsYouView = RouteAnswer.SpecShaped("needs-you", "extra-nothing");

        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Home);
        Settle(window);
        FrameFile.Save(window, string.Create(CultureInfo.InvariantCulture, $"h3212-needs-you-empty-{width}"));

        var home = window.GetVisualDescendants().OfType<HomePage>().Single(h => h.IsEffectivelyVisible);
        var line = home.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "NeedsYouEmpty");
        Assert.True(line.IsEffectivelyVisible);
        Assert.Equal(TextAlignment.Left, line.TextAlignment);

        // The card's StackPanel is the line's parent; the line's left edge sits 24 in from it.
        var card = Assert.IsType<Visual>(line.GetVisualParent(), exactMatch: false);
        var left = Assert.NotNull(line.TranslatePoint(default, card));
        Assert.Equal(24, left.X, precision: 1);
        window.Close();
    });
}
