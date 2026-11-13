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

        Assert.True(xs.Count >= 2, $"{xs.Count} copy commands");
        Assert.True(xs.Max() > card.Bounds.Width / 2, string.Create(CultureInfo.InvariantCulture, $"no command starts right of the card's middle ({card.Bounds.Width / 2}); starts: {string.Join(", ", xs)}"));
        Assert.True(xs.Min() < card.Bounds.Width / 2, "the first column is on the left");
    });

    // At 1280 the card is ~1230 px: two columns of 540 and a 12 px gap fit.
    [Fact]
    public Task At_1280_the_commands_sit_in_two_columns() => OnUi(async () =>
    {
        var (window, card) = await Actions(1280);
        var xs = CommandXs(window, card);

        Assert.True(xs.Count >= 2, $"{xs.Count} copy commands");
        Assert.True(xs.Max() > card.Bounds.Width / 2, $"no command starts right of the card's middle; starts: {string.Join(", ", xs.Select(x => x.ToString(CultureInfo.InvariantCulture)))}");
    });

    // The list is sorted by label, so it reads down the left column first: the first ceil(n / 2) commands are on the left, in catalog order, the rest on the right.
    [Theory]
    [InlineData(1280)]
    [InlineData(1440)]
    public Task The_commands_read_down_the_left_column_then_down_the_right(double width) => OnUi(async () =>
    {
        var (window, card) = await Actions(width);
        var points = CommandPoints(window, card);
        var left = (points.Count + 1) / 2;

        Assert.True(points.Count >= 3, $"{points.Count} copy commands");
        Assert.True(points.Take(left).All(p => p.X < card.Bounds.Width / 2), "the first half is in the left column");
        Assert.True(points.Skip(left).All(p => p.X > card.Bounds.Width / 2), "the rest is in the right column");
        Assert.True(points.Take(left).Zip(points.Take(left).Skip(1), (a, b) => b.Y > a.Y).All(down => down), "the left column runs top to bottom in catalog order");
        Assert.True(points.Skip(left).Zip(points.Skip(left + 1), (a, b) => b.Y > a.Y).All(down => down), "the right column runs top to bottom in catalog order");
    });

    [Fact]
    public Task At_1000_the_commands_sit_in_one_column() => OnUi(async () =>
    {
        var (window, card) = await Actions(1000);
        var xs = CommandXs(window, card);

        Assert.True(xs.Count >= 2, $"{xs.Count} copy commands");
        Assert.True(xs.Max() - xs.Min() < 1, $"commands start at {string.Join(", ", xs.Select(x => x.ToString(CultureInfo.InvariantCulture)))}");
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
        [.. Named<ItemsControl>(window, "CopyOnlyActions").GetVisualDescendants().OfType<CommandControl>()
            .Select(c => c.TranslatePoint(new Point(0, 0), card)!.Value.X)];

    private static List<Point> CommandPoints(Window window, Control card) =>
        [.. Named<ItemsControl>(window, "CopyOnlyActions").GetVisualDescendants().OfType<CommandControl>()
            .Select(c => c.TranslatePoint(new Point(0, 0), card)!.Value)];
}
