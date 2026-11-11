using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The review page's diff (#2776): a PR with a thousand diff lines builds only the line rows in view, not one Border, Grid and four TextBlocks per line, inside the real shell page host (the shared Pages ScrollViewer above it).</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ReviewDiffVirtualizationTests : FrameHost
{
    private const int Lines = 1000;

    // The full golden with its first file's patch replaced by one hunk of a thousand context lines.
    private static RouteAnswer BigDiff()
    {
        var page = JsonNode.Parse(GoldenFiles.Api("reviews-pr", "full"))!;   // why: the golden is a JSON document
        var patch = new StringBuilder("@@ -1,").Append(Lines.ToString(CultureInfo.InvariantCulture)).Append(" +1,").Append(Lines.ToString(CultureInfo.InvariantCulture)).Append(" @@");
        for (var i = 1; i <= Lines; i++)
        {
            _ = patch.Append("\n line ").Append(i.ToString(CultureInfo.InvariantCulture));
        }

        var file = page["diff"]!["groups"]![0]!["files"]![0]!;
        file["patch"] = patch.ToString();
        file["additions"] = 0;
        file["deletions"] = 0;
        return new RouteAnswer(HttpStatusCode.OK, page.ToJsonString());
    }

    [Fact]
    public Task The_diff_builds_only_the_rows_in_view() => OnUi(async () =>
    {
        Server.ReviewPageAnswer = BigDiff();
        var (window, shell) = await Connected(width: 1280, height: 800);
        shell.Navigate(DeepLink.ForReview("acme/api", 2071));
        await Until(window, () => shell.IsReviewPageShown && shell.ReviewPage.State == ReviewPageState.Ready, "the review page never reached Ready");
        Settle(window);

        var realized = window.GetVisualDescendants().OfType<Border>().Count(b => string.Equals(b.Name, "ReviewDiffLine", StringComparison.Ordinal));

        Assert.True(realized > 0, "the diff shows some lines");
        Assert.True(realized < 100, string.Create(CultureInfo.InvariantCulture, $"{realized} of {Lines} diff lines were built: the list is not virtualized"));
    });
}
