using Avalonia.Controls;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Linear;
using Aw.Sindri.Ui.Views.Todo;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#3620: the live status and error row style is one app-level style that both Settings cards use; neither card keeps a copy.</summary>
[Lane(TestLane.Integration)]
public sealed class LiveRowStyleTests : FrameHost
{
    [Fact]
    public Task The_live_row_style_is_shared_and_neither_card_keeps_a_local_copy() => OnUi(() =>
    {
        var linear = new LinearConnectionSection();
        var todo = new TodoNoteCardSection();
        var flat = new TextBlock { Classes = { "live-row" } };
        var filled = new TextBlock { Classes = { "live-row", "filled" } };
        var gap = new Border { Classes = { "live-gap" } };
        var window = new Window { Content = new StackPanel { Children = { flat, filled, gap } } };
        window.Show();

        Assert.Equal((0d, double.PositiveInfinity, 16d), (flat.MaxHeight, filled.MaxHeight, gap.Height));
        Assert.Equal((0, 0), (linear.Styles.Count, todo.Styles.Count));
        window.Close();
        return Task.CompletedTask;
    });
}
