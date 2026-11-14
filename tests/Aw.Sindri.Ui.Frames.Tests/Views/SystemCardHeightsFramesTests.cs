using System.Globalization;
using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The System cards keep their own height (#3117, #3101): the Actions card takes a full-width row of its own, every other card is as tall as
/// its content (top-aligned, never stretched to a taller neighbour), and a card arranged at a width other than the one it was measured at is
/// measured again, so wrapped text fits.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class SystemCardHeightsFramesTests : FrameHost
{
    private const string Typical = """{"sources":[{"source":"github","phase":"live","usable":true,"complete":false,"lastSync":null,"lastCode":null,"pull":"running","records":200,"lastCommit":"2026-10-11T11:57:00Z"}],"concepts":{"enabled":true,"concepts":10,"multiSource":2,"queued":0,"covered":20,"embedded":30,"pending":30,"coverage":0.5,"watermark":1,"code":null}}""";

    private const string LongText = "A card whose text wraps: the control plane is running, the profile is valid, the views are current and every index has finished its last pass without a single error.";

    // At 1440 the last row was Agents, Services and Actions: Agents and Services were stretched to Actions' ~600 px and left almost empty.
    [Theory]
    [InlineData(1000)]
    [InlineData(1280)]
    [InlineData(1440)]
    public Task The_actions_card_takes_its_own_full_width_row(double width) => OnUi(async () =>
    {
        var (window, cards, grid) = await SystemCards(width);
        var actions = ActionsCard(window, grid);

        var row = cards.Where(c => Math.Abs(c.Bounds.Y - actions.Bounds.Y) < 0.5).ToList();
        Assert.True(row.Count == 1, $"Actions shares its row with {string.Join(", ", row.Where(c => c != actions).Select(Describe))}");
        Assert.True(Math.Abs(actions.Bounds.Width - grid.Bounds.Width) < 0.5, string.Create(CultureInfo.InvariantCulture, $"Actions is {actions.Bounds.Width} wide, the grid is {grid.Bounds.Width}"));
    });

    [Theory]
    [InlineData(1000)]
    [InlineData(1280)]
    [InlineData(1440)]
    public Task Every_card_is_as_tall_as_its_content(double width) => OnUi(async () =>
    {
        var (_, cards, _) = await SystemCards(width);

        Assert.All(cards, c => Assert.True(
            c.Bounds.Height <= c.DesiredSize.Height + 0.5,
            string.Create(CultureInfo.InvariantCulture, $"{Describe(c)} is {c.Bounds.Height} tall, its content needs {c.DesiredSize.Height}")));
    });

    // #3101: measured at 852 (two 420 px columns), arranged at 840 (one 840 px column). The card kept the height of its text wrapped at 420.
    [Fact]
    public Task A_card_arranged_at_another_width_is_measured_again() => OnUi(() =>
    {
        var card = Wrapping();
        var grid = new CardGrid { Children = { card, Wrapping() } };
        grid.Measure(new Size(852, double.PositiveInfinity));
        grid.Arrange(new Rect(0, 0, 840, grid.DesiredSize.Height));

        var reference = Wrapping();
        reference.Measure(new Size(840, double.PositiveInfinity));

        Assert.Equal(840, card.Bounds.Width, precision: 3);
        Assert.Equal(reference.DesiredSize.Height, card.Bounds.Height, precision: 3);
        return Task.CompletedTask;
    });

    private static TextBlock Wrapping() => new() { Text = LongText, TextWrapping = TextWrapping.Wrap, FontSize = 15 };

    private async Task<(Window Window, List<Control> Cards, Panel Grid)> SystemCards(double width)
    {
        Server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Typical);
        var (window, shell) = await Connected(width: width);
        shell.GoTo(PageId.System);
        Settle(window);

        var grid = Named<Panel>(window, "SystemCards");
        var cards = grid.Children.Where(c => c.IsVisible).ToList();
        Assert.True(cards.Count > 3, string.Create(CultureInfo.InvariantCulture, $"{cards.Count} cards"));
        return (window, cards, grid);
    }

    private static Control ActionsCard(Window window, Panel grid)
    {
        var eyebrow = Named<Control>(window, "ActionsEyebrow");
        var card = eyebrow.GetVisualAncestors().OfType<Control>().FirstOrDefault(a => a.GetVisualParent() == grid);
        Assert.NotNull(card);
        return card;
    }

    private static string Describe(Control card) =>
        card.Name ?? card.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Classes.Contains("section-title"))?.Text ?? card.GetType().Name;
}
