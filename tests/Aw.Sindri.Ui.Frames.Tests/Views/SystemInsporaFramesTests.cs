using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The System page's visual pass (#2779): one display number, section titles on borderless 24 px cards, and the Context index backlog as ticks.</summary>
[Lane(TestLane.Integration)]
public sealed class SystemInsporaFramesTests : FrameHost
{
    private const string Typical = """{"sources":[{"source":"github","phase":"live","usable":true,"complete":false,"lastSync":null,"lastCode":null,"pull":"running","records":200,"lastCommit":"2026-10-11T11:57:00Z"}],"concepts":{"enabled":true,"concepts":10,"multiSource":2,"queued":0,"covered":20,"embedded":30,"pending":30,"coverage":0.5,"watermark":1,"code":null}}""";

    // A first run: nothing pulled, nothing embedded, so the backlog bar has no records to draw (the default fake answer has 562).
    private const string Empty = """{"sources":[],"concepts":{"enabled":true,"concepts":0,"multiSource":0,"queued":0,"covered":0,"embedded":0,"pending":0,"coverage":0,"watermark":0,"code":null}}""";

    [Theory]
    [InlineData(1280, true)]
    [InlineData(1280, false)]
    [InlineData(1000, true)]
    [InlineData(1000, false)]
    public Task The_page_has_one_display_number_section_titles_and_borderless_cards(double width, bool typical) => OnUi(async () =>
    {
        Server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, typical ? Typical : Empty);

        var (window, shell) = await Connected(width: width);
        shell.GoTo(PageId.System);
        Settle(window);

        var overview = Named<StackPanel>(window, "OverviewPanel");
        var displays = overview.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Classes.Contains("display")).ToList();
        _ = Assert.Single(displays);
        Assert.Equal("HeadlineWord", displays[0].Name);

        var panels = overview.GetVisualDescendants().OfType<Border>().Where(b => b.IsEffectivelyVisible && b.Classes.Contains("panel")).ToList();
        Assert.NotEmpty(panels);
        Assert.All(panels, p =>
        {
            Assert.Equal(new Thickness(0), p.BorderThickness);
            var inset = p.Child is Border section && section.Classes.Contains("section") ? section.Padding : p.Padding;   // why: a card's inset is on its section (#2842); the headline carries its own
            Assert.Equal(new Thickness(24), inset);
        });

        var titles = overview.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Classes.Contains("section-title")).ToList();
        Assert.NotEmpty(titles);
        Assert.All(titles, t => Assert.Equal(15, t.FontSize));
        Assert.Equal(typical, Visible(window, "ContextIndexBacklog"));

        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"system-inspora-{width:0}-{(typical ? "typical" : "empty")}"), prefix: "");

        var indexPanel = Named<Control>(window, "ContextIndexPanel");
        indexPanel.BringIntoView();   // why: the card with the backlog bar sits below the first 800 px
        Settle(window);
        if (typical)
        {
            BacklogEndsInsideTheCard(window, indexPanel);
        }

        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"system-inspora-{width:0}-{(typical ? "typical" : "empty")}-index"), prefix: "");
    });

    // #2905: the cards fill the row. At 1280 two fixed 440 px cards ended at 892 of 1232 and left a 340 px strip. Now every full row ends
    // within 24 px of the content edge, as many columns as fit at 420 px or more (the 356 px backlog bar + the 24 px insets need 404).
    [Theory]
    [InlineData(1000, 2)]
    [InlineData(1280, 2)]
    [InlineData(1440, 3)]
    public Task The_cards_fill_each_row_to_the_content_edge(double width, int columns) => OnUi(async () =>
    {
        Server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Typical);

        var (window, shell) = await Connected(width: width);
        shell.GoTo(PageId.System);
        Settle(window);

        var overview = Named<StackPanel>(window, "OverviewPanel");
        var cards = Named<Panel>(window, "SystemCards").Children.Where(c => c.IsVisible).ToList();
        var culture = System.Globalization.CultureInfo.InvariantCulture;

        // The shots come before the asserts, so the same test shoots main's layout too (the before frames of #3098).
        Frame(window, string.Create(culture, $"system-grid-{width:0}"), prefix: "gh2905-");
        var scroll = overview.FindAncestorOfType<ScrollViewer>();
        Assert.NotNull(scroll);
        scroll.ScrollToEnd();
        Settle(window);
        Frame(window, string.Create(culture, $"system-grid-{width:0}-end"), prefix: "gh2905-");
        scroll.Offset = default;
        Settle(window);

        Assert.True(cards.Count > columns, string.Create(culture, $"{cards.Count} cards, too few to fill a row of {columns}"));
        var rows = cards.Select(c => (Card: c, At: c.TranslatePoint(default, overview)!.Value))
            .GroupBy(c => Math.Round(c.At.Y, MidpointRounding.AwayFromZero)).Select(r => r.ToList()).ToList();
        Assert.Equal(columns, rows[0].Count);
        var edge = overview.Bounds.Width;
        foreach (var row in rows.Where(r => r.Count == columns))
        {
            var (card, at) = row.MaxBy(c => c.At.X);
            var right = at.X + card.Bounds.Width;
            Assert.True(Math.Abs(edge - right) <= 24, string.Create(culture, $"a row of {columns} ends at {right}, the content edge is {edge}"));
        }

        Assert.All(cards, c => Assert.True(c.Bounds.Width >= 404, string.Create(culture, $"{c.Name} is {c.Bounds.Width} wide, under the 404 the backlog bar needs")));
    });

    // The ticks and their caption end inside the card's 24 px inset (side by side, the caption ran past the card and was clipped).
    private static void BacklogEndsInsideTheCard(Window window, Control indexPanel)
    {
        var ticks = Named<Control>(window, "ContextIndexTicks");
        var caption = Named<TextBlock>(window, "ContextIndexBacklogText");
        var ticksRight = ticks.TranslatePoint(new Point(ticks.Bounds.Width, 0), indexPanel)!.Value.X;
        var captionRight = caption.TranslatePoint(new Point(caption.TextLayout.Width, 0), indexPanel)!.Value.X;
        var limit = indexPanel.Bounds.Width - 24;
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        Assert.True(ticksRight <= limit + 0.5, string.Create(culture, $"ticks end at {ticksRight}, the card's content ends at {limit}"));
        Assert.True(captionRight <= limit + 0.5, string.Create(culture, $"caption ends at {captionRight}, the card's content ends at {limit}"));
    }
}
