using System.Text.RegularExpressions;
using Avalonia.Controls;
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
        new(System.Net.HttpStatusCode.OK, RepoValue().Replace(GoldenFiles.SpecShaped("queue", "fixture"), "\"repo\": null"));

    [GeneratedRegex("\"repo\": *\"[^\"]*\"", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RepoValue();

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

    [Fact]
    public Task At_1000_px_stage_goes_before_age_and_age_is_right_aligned_and_muted() => OnUi(async () =>
    {
        var (window, header) = await Table(1000);   // the fixture's rows have repos, so Repo stays

        Assert.Equal(["Title", "Repo", "Status", "Age"], Shown(header), StringComparer.Ordinal);
        var age = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "AgeText" && t.IsEffectivelyVisible);
        Assert.Equal(TextAlignment.Right, age.TextAlignment);
        Assert.Contains("subtle", age.Classes, StringComparer.Ordinal);
        Frame(window, "work-table-1000");
    });
}
