using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Work table at 1000 px (the window minimum) and 1280 px (issue 2551): the columns left are the ones with something to say, Age is always there, right-aligned and muted, and the id is not in the title.</summary>
[Lane(TestLane.Integration)]
public sealed partial class QueueWorkTableWidthTests : FrameHost
{
    // The fixture queue with every row's repo taken out: no golden has rows without a repo.
    private static RouteAnswer NoRepos() =>
        new(System.Net.HttpStatusCode.OK, RepoValue.Replace(GoldenFiles.SpecShaped("queue", "fixture"), "\"repo\": null"));

    [GeneratedRegex("\"repo\": *\"[^\"]*\"", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RepoValue { get; }

    private async Task<(Window Window, Grid Header)> Table(double width)
    {
        var (window, shell) = await Connected(s => !s.Queue.IsLoading, width: width, height: 800);
        shell.GoTo(PageId.Work);
        Settle(window);
        return (window, Named<Grid>(window, "QueueHeader"));
    }

    private static List<string?> Shown(Grid header) => [.. header.Children.OfType<TextBlock>().Where(t => t.IsVisible).Select(t => t.Text)];

    [Fact]
    public Task At_1280_px_the_empty_repo_column_is_gone_and_age_stays() => OnUi(async () =>
    {
        Server.QueueView = NoRepos();
        var (window, header) = await Table(1280);

        Assert.Equal(["Title", "Status", "Stage", "Age"], Shown(header), StringComparer.Ordinal);
        Assert.False(Visible(window, "QueueFooter"));
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "IdText");
        Frame(window, "work-table-1280");
    });

    // #2551 qa: at 1000 px Stage was dropped while ~270 px of the table sat empty. The drop follows the room the columns need, not the window's width.
    [Fact]
    public Task At_1000_px_stage_stays_when_there_is_room_and_age_is_right_aligned_and_muted() => OnUi(async () =>
    {
        Server.QueueView = NoRepos();
        var (window, header) = await Table(1000);

        Assert.Equal(["Title", "Status", "Stage", "Age"], Shown(header), StringComparer.Ordinal);
        var age = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "AgeText" && t.IsEffectivelyVisible);
        Assert.Equal(TextAlignment.Right, age.TextAlignment);
        Assert.Contains("subtle", age.Classes, StringComparer.Ordinal);
        Frame(window, "work-table-1000");
    });

    // #2551 qa: Age's header was left-aligned while its values were right-aligned: the header's right edge is the values'.
    [Theory]
    [InlineData(1000)]
    [InlineData(1280)]
    public Task Age_header_and_values_share_their_right_edge_at(int width) => OnUi(async () =>
    {
        Server.QueueView = NoRepos();
        var (window, header) = await Table(width);

        var head = header.Children.OfType<TextBlock>().First(t => string.Equals(t.Text, "Age", StringComparison.Ordinal));
        var value = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "AgeText" && t.IsEffectivelyVisible);
        var headEdge = head.TranslatePoint(new Point(head.Bounds.Width, 0), window)!.Value.X;
        var valueEdge = value.TranslatePoint(new Point(value.Bounds.Width, 0), window)!.Value.X;
        Assert.InRange(Math.Abs(headEdge - valueEdge), 0, 1);
        Assert.Equal(HorizontalAlignment.Right, head.HorizontalAlignment);
        Assert.Equal(HorizontalAlignment.Right, value.HorizontalAlignment);
    });
}
