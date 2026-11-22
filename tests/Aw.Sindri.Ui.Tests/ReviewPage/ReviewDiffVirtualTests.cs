using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Reviews;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.ReviewPage;

/// <summary>The review diff in the window builds only the rows in view, however many lines the PR changes, and the diff keys still reach a row that was never built (#2776).</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ReviewDiffVirtualTests : FrameHost
{
    private const int FileTotal = 50;

    private const int LinesPerFile = 100;

    // The full golden with one open group of 50 added files, 100 lines each: 5,000 diff lines.
    private static string BigDiff()
    {
        var json = ReviewPageFixture.Golden("full");
        var files = new JsonArray();
        for (var f = 0; f < FileTotal; f++)
        {
            var patch = new StringBuilder(string.Create(CultureInfo.InvariantCulture, $"@@ -0,0 +1,{LinesPerFile} @@"));
            for (var l = 1; l <= LinesPerFile; l++)
            {
                patch.Append(CultureInfo.InvariantCulture, $"\n+var line{l} = {l};");
            }

            files.Add(new JsonObject
            {
                ["path"] = FilePath(f),
                ["status"] = "added",
                ["previousPath"] = null,
                ["similarity"] = null,
                ["additions"] = LinesPerFile,
                ["deletions"] = 0,
                ["patch"] = patch.ToString(),
                ["omitted"] = null,
                ["note"] = null,
                ["url"] = "https://github.com/acme/api/pull/2071/files#diff-" + f.ToString(CultureInfo.InvariantCulture),
            });
        }

        json["diff"]!["fileCount"] = FileTotal;
        json["diff"]!["groups"] = new JsonArray(new JsonObject { ["name"] = "Core", ["folded"] = false, ["files"] = files });
        return json.ToJsonString();
    }

    private static string FilePath(int file) => string.Create(CultureInfo.InvariantCulture, $"src/Big/F{file:00}.cs");

    private async Task<(ShellWindow Window, ShellViewModel Shell)> OnBigPage()
    {
        Server.ReviewPageAnswer = new RouteAnswer(HttpStatusCode.OK, BigDiff());
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.Navigate(DeepLink.ForReview("acme/api", 2071));
        await Until(window, () => shell.IsReviewPageShown && shell.ReviewPage.State == ReviewPageState.Ready && Visible(window, "ReviewDiffLine"), "the review page never showed its diff");
        Settle(window);
        return (window, shell);
    }

    private static int BuiltLines(Window window) => window.GetVisualDescendants().OfType<Control>().Count(c => c.Name == "ReviewDiffLine");

    [Fact]
    public Task A_5000_line_diff_builds_only_the_rows_in_view() => OnUi(async () =>
    {
        var (window, shell) = await OnBigPage();

        var built = BuiltLines(window);

        Assert.Equal(FileTotal * LinesPerFile, shell.ReviewPage.Diff.Rows.Count(r => r.Kind == ReviewDiffRowKind.Line));
        Assert.True(built is > 0 and < 200, string.Create(CultureInfo.InvariantCulture, $"the diff built {built} of {FileTotal * LinesPerFile} lines"));
    });

    [Fact]
    public Task J_to_the_last_file_focuses_its_first_line_though_that_row_was_never_built() => OnUi(async () =>
    {
        var (window, shell) = await OnBigPage();
        var target = FilePath(FileTotal - 1) + ":+1";
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "ReviewDiffLine" && string.Equals(c.Tag as string, target, StringComparison.Ordinal));

        for (var i = 0; i < FileTotal; i++)
        {
            _ = shell.ReviewPage.PressDiffKey("j", focusInDiff: true, textBoxHasFocus: false);
        }

        await Until(
            window,
            () => TopLevel.GetTopLevel(window)?.FocusManager?.GetFocusedElement() is Control { DataContext: ReviewDiffLineViewModel line } focused
                && string.Equals(focused.Name, "ReviewDiffLine", StringComparison.Ordinal) && string.Equals(line.Key, target, StringComparison.Ordinal),
            "focus never reached the last file's first line");
        Assert.True(BuiltLines(window) < 200);
    });

    private static IInputElement? Focused(Window window) => TopLevel.GetTopLevel(window)?.FocusManager?.GetFocusedElement();

    private static bool HoldsLine(Window window, string key) => Focused(window) is Control { DataContext: ReviewDiffLineViewModel line } focused && string.Equals(focused.Name, "ReviewDiffLine", StringComparison.Ordinal) && string.Equals(line.Key, key, StringComparison.Ordinal);

    private static ScrollViewer PageScroller(Window window) => window.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.GetVisualDescendants().OfType<Control>().Any(c => c.Name == "ReviewDiffRows"));

    private static async Task ScrollPageTo(Window window, bool end) => await ScrollPageTo(window, end ? PageScroller(window).Extent.Height : 0);

    private static async Task ScrollPageTo(Window window, double offset)
    {
        PageScroller(window).Offset = new Avalonia.Vector(0, offset);
        Settle(window);
        await Task.Yield();
        Settle(window);
    }

    private async Task<(Window Window, ReviewPageViewModel Page, string Key)> FocusedFirstLine()
    {
        var (window, shell) = await OnBigPage();
        var page = shell.ReviewPage;
        _ = page.PressDiffKey("j", focusInDiff: true, textBoxHasFocus: false);   // the group header
        _ = page.PressDiffKey("j", focusInDiff: true, textBoxHasFocus: false);   // the first file
        _ = page.PressDiffKey("j", focusInDiff: true, textBoxHasFocus: false);   // its first line
        var key = page.Focus.Key;
        await Until(window, () => HoldsLine(window, key), "focus never reached the first line");
        return (window, page, key);
    }

    [Fact]
    public Task A_focused_row_that_scrolls_out_and_back_holds_keyboard_focus_again() => OnUi(async () =>
    {
        var (window, _, key) = await FocusedFirstLine();
        var focusedAt = PageScroller(window).Offset.Y;   // the j presses scrolled the focused row into view; it is not at the top

        await ScrollPageTo(window, end: true);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "ReviewDiffLine" && string.Equals(c.Tag as string, key, StringComparison.Ordinal));
        await ScrollPageTo(window, focusedAt);

        Assert.Contains(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "ReviewDiffLine" && string.Equals(c.Tag as string, key, StringComparison.Ordinal));
        await Until(window, () => HoldsLine(window, key), "the focused row did not take keyboard focus back when it was built again");
    });

    [Fact]
    public Task A_diff_key_still_moves_the_focus_while_the_focused_row_is_out_of_the_built_range() => OnUi(async () =>
    {
        var (window, page, key) = await FocusedFirstLine();

        await ScrollPageTo(window, end: true);
        window.KeyPress(Key.J, RawInputModifiers.None, PhysicalKey.J, keySymbol: "j");
        Settle(window);

        Assert.NotEqual(key, page.Focus.Key, StringComparer.Ordinal);
        var moved = page.Focus.Key;
        await Until(window, () => HoldsLine(window, moved), "the next row never took keyboard focus after the key");
    });

    [Fact]
    public Task Show_on_a_deleted_file_hands_keyboard_focus_to_the_files_path_row() => OnUi(async () =>
    {
        Server.ReviewPageAnswer = new RouteAnswer(HttpStatusCode.OK, ReviewPageFixture.Golden("full").ToJsonString());
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.Navigate(DeepLink.ForReview("acme/api", 2071));
        await Until(window, () => shell.IsReviewPageShown && shell.ReviewPage.State == ReviewPageState.Ready && Visible(window, "ReviewDiffLine"), "the review page never showed its diff");
        var deleted = shell.ReviewPage.Diff.Groups.SelectMany(g => g.Files).Single(f => f.HasShow);
        var rows = window.GetVisualDescendants().OfType<ItemsControl>().First(c => c.Name == "ReviewDiffRows");
        rows.ScrollIntoView(shell.ReviewPage.Diff.RowOf(ReviewFocusRegion.FileNote, deleted.Path));
        Button? show = null;
        await Until(window, () => (show = window.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => string.Equals(b.Name, "ReviewDiffShow", StringComparison.Ordinal) && ReferenceEquals(b.DataContext, deleted) && b.IsEffectivelyVisible)) is not null, "the Show button never appeared");
        Assert.True(show!.Focus(NavigationMethod.Tab));

        show.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        await Until(window, () => Focused(window) is Control { DataContext: ReviewDiffFileViewModel file } focused && string.Equals(focused.Name, "ReviewDiffFile", StringComparison.Ordinal) && ReferenceEquals(file, deleted), "the file row never took keyboard focus after Show");
    });
}
