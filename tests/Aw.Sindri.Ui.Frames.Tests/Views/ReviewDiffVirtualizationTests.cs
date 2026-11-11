using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
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
    private const int HunkLines = 50;

    // The full golden with its first file's patch replaced by one hunk of a thousand context lines.
    private static RouteAnswer BigDiff()
    {
        var page = JsonNode.Parse(GoldenFiles.Api("reviews-pr", "full"))!;   // why: the golden is a JSON document
        // twenty hunks of fifty context lines, ten lines apart
        var patch = new StringBuilder();
        for (var hunk = 0; hunk < Lines / HunkLines; hunk++)
        {
            var start = 1 + (hunk * (HunkLines + 10));
            _ = patch.Append(hunk == 0 ? string.Empty : "\n").Append(string.Create(CultureInfo.InvariantCulture, $"@@ -{start},{HunkLines} +{start},{HunkLines} @@"));
            for (var i = 0; i < HunkLines; i++)
            {
                _ = patch.Append("\n line ").Append((start + i).ToString(CultureInfo.InvariantCulture));
            }
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

    // n jumps to a hunk far below the realized window: its first line must be built, focused and on screen (a guard the flat, virtualized list must keep).
    [Fact]
    public Task A_jump_to_a_far_hunk_focuses_its_line_and_scrolls_it_into_view() => OnUi(async () =>
    {
        Server.ReviewPageAnswer = BigDiff();
        var (window, shell) = await Connected(width: 1280, height: 800);
        shell.Navigate(DeepLink.ForReview("acme/api", 2071));
        await Until(window, () => shell.IsReviewPageShown && shell.ReviewPage.State == ReviewPageState.Ready, "the review page never reached Ready");
        Settle(window);

        for (var i = 0; i < 15; i++)
        {
            Assert.True(shell.ReviewPage.PressDiffKey("n", focusInDiff: true, textBoxHasFocus: false));
        }

        var line = shell.ReviewPage.Diff.FocusedLine;
        Assert.NotNull(line);
        Settle(window);

        var focused = window.FocusManager?.GetFocusedElement() as Control;
        Assert.True(focused is Border border && string.Equals(border.Name, "ReviewDiffLine", StringComparison.Ordinal) && string.Equals(border.Tag as string, line.Key, StringComparison.Ordinal), "the far hunk's first line never took focus");
        var top = focused.TranslatePoint(default, window)!.Value.Y;   // why: both are in the window
        Assert.True(top >= 0 && top <= window.Height, string.Create(CultureInfo.InvariantCulture, $"the focused line is at y={top}, outside the {window.Height} px window"));
    });
}
