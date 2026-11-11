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
