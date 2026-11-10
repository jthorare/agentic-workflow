using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The System overview's layout (#2554): the card grid fills the width, and a service name is cut with an ellipsis before the interval column.</summary>
[Lane(TestLane.Integration)]
public sealed class SystemPageLayoutTests : FrameHost
{
    [Theory]
    [InlineData(1280d, 3)]
    [InlineData(900d, 2)]
    public Task The_card_grid_fills_the_width_in_two_or_three_columns(double width, int columns) => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: width);
        shell.GoTo(PageId.System);
        Settle(window);

        var panel = Named<WrapPanel>(window, "SystemCards");
        var cards = panel.Children.OfType<Control>().Where(c => c.IsEffectivelyVisible).ToList();
        Assert.Equal(columns, cards.Select(c => Math.Round(c.Bounds.X)).Distinct().Count());
        Assert.True(cards.Max(c => c.Bounds.Right) >= panel.Bounds.Width - 24, $"cards end at {cards.Max(c => c.Bounds.Right)} of {panel.Bounds.Width}");
    });

    [Fact]
    public Task A_service_name_is_trimmed_with_an_ellipsis_and_never_runs_into_the_interval() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.Health.ServiceCard.Count > 0, width: 900);
        shell.GoTo(PageId.System);
        Settle(window);

        var list = Named<ItemsControl>(window, "ServicesList");
        var row = list.GetVisualDescendants().OfType<Grid>().First(g => g.IsEffectivelyVisible && g.Children.Count == 3);
        var name = (TextBlock)row.Children[0];
        var every = (TextBlock)row.Children[1];
        Assert.Equal(TextTrimming.CharacterEllipsis, name.TextTrimming);
        Assert.True(name.Bounds.Right <= every.Bounds.X + 0.5, $"name ends at {name.Bounds.Right}, interval starts at {every.Bounds.X}");
        Assert.NotNull(ToolTip.GetTip(name));
    });
}
