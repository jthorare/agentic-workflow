using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Agents;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>Every session row in the Agents table is one line high (#2553): a row with no step says "—" with no extra margin, so it is no taller than a row that asks something, and the dash sits left like the other cells.</summary>
[Lane(TestLane.Integration)]
public sealed class AgentsRowHeightTests : FrameHost
{
    [Theory]
    [InlineData(1000)]
    [InlineData(1280)]
    public Task Every_session_row_is_one_line_high_and_the_dash_is_left_aligned_at(int width) => OnUi(async () =>
    {
        Server.SessionAnswer = RouteAnswer.Api("sessions", "fixture-three-providers");
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.Groups.Count > 0);
        Settle(window);

        var rows = window.GetVisualDescendants().OfType<SessionRow>().Where(r => r.IsEffectivelyVisible).ToList();
        Assert.NotEmpty(rows);
        var heights = rows.Select(r => Math.Round(Named<Border>(r, "SessionRowFrame").Bounds.Height, MidpointRounding.AwayFromZero)).Distinct().ToList();
        var height = Assert.Single(heights);
        Assert.True(height <= 40, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"a session row is {height} px high: more than one line"));
        var dashes = rows.SelectMany(r => r.GetVisualDescendants().OfType<TextBlock>()).Where(t => t.IsEffectivelyVisible && string.Equals(t.Text, "—", StringComparison.Ordinal) && t.GetVisualAncestors().OfType<Control>().Any(a => a.Name == "SessionRowDoing")).ToList();
        Assert.All(dashes, d => Assert.NotEqual(HorizontalAlignment.Center, d.HorizontalAlignment));
        Assert.All(dashes, d => Assert.Equal(default, d.Margin));
    });
}
