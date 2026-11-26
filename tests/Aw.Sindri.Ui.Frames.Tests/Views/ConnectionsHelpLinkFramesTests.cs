using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// #4485: a tile's help link ("Open Spotify's developer dashboard") was wider than the 260 px tile and its label was cut off ("...dashb") with no wrap and no ellipsis. Every help link of every tile, as a token link and as an OAuth link,
/// shows its whole label inside the tile: the label's text fits the label's width (wrapped, if need be) and the button stays inside the tile's frame.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ConnectionsHelpLinkFramesTests : FrameHost
{
    [Theory]
    [InlineData("github", false)]
    [InlineData("linear", false)]
    [InlineData("slack", false)]
    [InlineData("github", true)]
    [InlineData("linear", true)]
    [InlineData("google", true)]
    [InlineData("spotify", true)]
    public Task The_help_link_label_is_not_cut_off_and_stays_inside_the_tile(string id, bool oauth) => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.GoTo(PageId.Connections);
        await Until(window, () => shell.Connections.Drawn > 0, "the Connections page never drew");
        var tile = shell.Connections.Tiles.Single(t => string.Equals(t.Id, id, StringComparison.Ordinal));
        await tile.Connect(CancellationToken.None);
        if (oauth && tile.ShowUseOAuth)
        {
            tile.UseOAuth();
        }

        Settle(window);
        var inTile = window.GetVisualDescendants().OfType<Control>().Where(c => c.GetVisualAncestors().OfType<Control>().Any(a => a.DataContext == tile)).ToList();
        var link = Assert.Single(inTile.OfType<Button>(), b => string.Equals(b.Name, "ServiceTileHelpLink", StringComparison.Ordinal) && b.IsEffectivelyVisible);
        var frame = Assert.Single(inTile.OfType<Border>(), b => string.Equals(b.Name, "ServiceTileFrame", StringComparison.Ordinal));
        var label = Assert.Single(link.GetVisualDescendants().OfType<TextBlock>());

        // why: Measure clamps DesiredSize to the space offered, so a cut-off NoWrap label still has Bounds.Width == DesiredSize.Width; the cut shows only in the text's own layout, which must fit the label and collapse no line
        var text = label.TextLayout;
        Assert.True(text.WidthIncludingTrailingWhitespace <= label.Bounds.Width + 0.5, string.Create(CultureInfo.InvariantCulture, $"{id} (oauth {oauth}): the label '{label.Text}' needs {text.WidthIncludingTrailingWhitespace:0} px of text but is laid out {label.Bounds.Width:0} px wide"));
        Assert.False(text.TextLines.Any(l => l.HasCollapsed), $"{id} (oauth {oauth}): the label '{label.Text}' is trimmed");
        var linkRight = link.TranslatePoint(new Point(link.Bounds.Width, 0), frame)!.Value.X;
        Assert.True(linkRight <= frame.Bounds.Width + 0.5, string.Create(CultureInfo.InvariantCulture, $"{id} (oauth {oauth}): the link ends at {linkRight:0} px, past the tile's {frame.Bounds.Width:0} px"));
        FrameFile.Save(window, $"connections-help-link-{id}-{(oauth ? "oauth" : "token")}");
    });
}
