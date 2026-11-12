using System.Globalization;
using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The System page's wide cards use their width (#3356, #3357): the full-width Actions card lays its commands out in two columns when two fit and in one below
/// that, and the Sandbox card's body line starts at its title's left edge instead of 24 px in (the state mark sits in the heading beside the state word).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class SystemActionsColumnsFramesTests : FrameHost
{
    private const string Typical = """{"sources":[{"source":"github","phase":"live","usable":true,"complete":false,"lastSync":null,"lastCode":null,"pull":"running","records":200,"lastCommit":"2026-10-11T11:57:00Z"}],"concepts":{"enabled":true,"concepts":10,"multiSource":2,"queued":0,"covered":20,"embedded":30,"pending":30,"coverage":0.5,"watermark":1,"code":null}}""";

    // At 1440 the card is ~1390 px and the commands stopped at ~650: the right half was empty.
    [Fact]
    public Task At_1440_the_commands_sit_in_two_columns() => OnUi(async () =>
    {
        var (window, card) = await Actions(1440);
        var xs = CommandXs(window, card);

        Assert.True(xs.Count >= 2, string.Create(CultureInfo.InvariantCulture, $"{xs.Count} copy commands"));
        Assert.True(xs.Max() > card.Bounds.Width / 2, string.Create(CultureInfo.InvariantCulture, $"no command starts right of the card's middle ({card.Bounds.Width / 2}); starts: {string.Join(", ", xs)}"));
        Assert.True(xs.Min() < card.Bounds.Width / 2, "the first column is on the left");
    });

    [Fact]
    public Task At_1000_the_commands_sit_in_one_column() => OnUi(async () =>
    {
        var (window, card) = await Actions(1000);
        var xs = CommandXs(window, card);

        Assert.True(xs.Count >= 2, string.Create(CultureInfo.InvariantCulture, $"{xs.Count} copy commands"));
        Assert.True(xs.Max() - xs.Min() < 1, string.Create(CultureInfo.InvariantCulture, $"commands start at {string.Join(", ", xs)}"));
    });

    [Theory]
    [InlineData(1000)]
    [InlineData(1440)]
    public Task The_sandbox_body_starts_at_its_titles_left_edge(double width) => OnUi(async () =>
    {
        Server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Typical);
        var (window, shell) = await Connected(width: width);
        shell.GoTo(PageId.System);
        Settle(window);

        var title = Named<TextBlock>(window, "SandboxEyebrow");
        var body = Named<TextBlock>(window, "SandboxRow");
        var titleX = title.TranslatePoint(new Point(0, 0), window)!.Value.X;
        var bodyX = body.TranslatePoint(new Point(0, 0), window)!.Value.X;

        Assert.True(Math.Abs(titleX - bodyX) < 0.5, string.Create(CultureInfo.InvariantCulture, $"the title starts at {titleX}, the body at {bodyX}"));
    });

    private async Task<(Window Window, Control Card)> Actions(double width)
    {
        Server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Typical);
        var (window, shell) = await Connected(width: width);
        shell.GoTo(PageId.System);
        Settle(window);

        var grid = Named<Panel>(window, "SystemCards");
        var eyebrow = Named<Control>(window, "ActionsEyebrow");
        var card = eyebrow.GetVisualAncestors().OfType<Control>().FirstOrDefault(a => a.GetVisualParent() == grid);
        Assert.NotNull(card);
        return (window, card);
    }

    // Where each copy-only command starts, across the card.
    private static List<double> CommandXs(Window window, Control card) =>
        Named<ItemsControl>(window, "CopyOnlyActions").GetVisualDescendants().OfType<CommandControl>()
            .Select(c => c.TranslatePoint(new Point(0, 0), card)!.Value.X)
            .ToList();
}
