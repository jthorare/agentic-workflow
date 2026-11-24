using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Projects;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The Projects rows in the window at 1280 and 1000 px (issue 3717, frames round 11): a long name trims before the PRs column, the health groups' heads
/// show in their fixed order, and a failed truth read shows its one line and state groups instead (frames round 13, truth-failed). Saved under frames/ as projects-rows-*.png.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ProjectsRowsFramesTests : FrameHost
{
    private static readonly string[] _barButtons = ["ProjectsSort", "ProjectsGroup", "ProjectsAdd"];

    private const string LongName = "Enterprise onboarding experience overhaul for every tenant and every region we sell in, with the admin console, the billing hand-off and the support runbooks rewritten to match";

    private static JsonObject Truth(string? level) => new()
    {
        ["openPrs"] = new JsonArray(),
        ["openItems"] = 1,
        ["doneItems"] = 2,
        ["totalItems"] = 3,
        ["runningAgents"] = 0,
        ["health"] = level is null ? null : new JsonObject { ["level"] = level, ["reason"] = "Off track: 2 of 4 PRs red, and a reason long enough to need trimming" },
    };

    // The mixed golden with truth: Checkout rewrite off track (and a long name), Kitchen CI not checked, Mobile app no open PRs.
    private static string Served(bool factsMissing)
    {
        var list = JsonNode.Parse(GoldenFiles.Api("projects", "mixed"))!;   // why: the golden is a JSON document
        var projects = list["projects"]!.AsArray();
        projects[0]!["name"] = LongName;
        projects[0]!["truth"] = Truth(factsMissing ? null : "offTrack");
        projects[1]!["truth"] = Truth(factsMissing ? null : "unknown");
        projects[2]!["truth"] = Truth(level: null);
        if (factsMissing)
        {
            list["freshness"]!.AsArray().Add(new JsonObject { ["source"] = "sindri", ["state"] = "failing", ["text"] = "SND-REVIEWS-012: reviews.db is locked", ["at"] = null });
        }

        return list.ToJsonString();
    }

    private async Task<(Window Window, ShellViewModel Shell)> OnProjects(int width, int height, bool factsMissing = false)
    {
        Server.ProjectsAnswer = new RouteAnswer(HttpStatusCode.OK, Served(factsMissing));
        var (window, shell) = await Connected(width: width, height: height);
        shell.Projects.SetView(ViewModels.Projects.ProjectView.All);   // why: the golden's finished and hidden cards carry blockedOnYou, so an unpicked view opens on Needs me, which has no row here
        shell.GoTo(PageId.Projects);
        await Until(window, () => !shell.Projects.IsLoading && shell.Projects.HasRows, "the Projects page never drew its rows");
        Settle(window);
        return (window, shell);
    }

    private static double Left(Control control, Visual ancestor) => control.TranslatePoint(default, ancestor)?.X ?? double.NaN;

    private static double Right(Control control, Visual ancestor) => control.TranslatePoint(new Point(control.Bounds.Width, 0), ancestor)?.X ?? double.NaN;

    [Theory]
    [InlineData(1280, 800)]
    [InlineData(1000, 700)]
    public Task A_long_name_ends_before_the_PRs_column(int width, int height) => OnUi(async () =>
    {
        var (window, _) = await OnProjects(width, height);

        var row = window.GetVisualDescendants().OfType<ProjectRow>().First(r => r.IsEffectivelyVisible && Named<TextBlock>(r, "ProjectRowNameText").Text == LongName);
        var name = Named<Border>(row, "ProjectRowName");
        var prs = Named<Border>(row, "ProjectRowPrs");

        Assert.True(Right(name, row) <= Left(prs, row) + 0.5, string.Create(CultureInfo.InvariantCulture, $"the name ends at {Right(name, row)}, past the PRs column at {Left(prs, row)}"));
        Assert.True(Named<TextBlock>(row, "ProjectRowNameText").TextLayout.TextLines.Any(l => l.HasCollapsed), "the long name is not trimmed with an ellipsis");
        Frame(window, string.Create(CultureInfo.InvariantCulture, $"projects-rows-long-name-{width}"), prefix: string.Empty);
    });

    [Theory]
    [InlineData(1280, 800)]
    [InlineData(1000, 700)]
    public Task The_health_group_heads_show_in_order(int width, int height) => OnUi(async () =>
    {
        var (window, _) = await OnProjects(width, height);

        var heads = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "ProjectsGroupHeading" && t.IsEffectivelyVisible).Select(t => t.Text).ToList();

        Assert.Equal(["Off track", "CI not checked", "No open PRs"], heads, StringComparer.Ordinal);
        Frame(window, string.Create(CultureInfo.InvariantCulture, $"projects-rows-{width}"), prefix: string.Empty);
    });

    [Theory]
    [InlineData(1280, 800)]
    [InlineData(1000, 700)]
    public Task A_failed_truth_read_shows_its_line_and_state_groups(int width, int height) => OnUi(async () =>
    {
        var (window, _) = await OnProjects(width, height, factsMissing: true);

        var heads = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "ProjectsGroupHeading" && t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.True(Visible(window, "ProjectsFactsMissing"));
        Assert.Equal(["In progress", "Planned", "Paused"], heads, StringComparer.Ordinal);
        Frame(window, string.Create(CultureInfo.InvariantCulture, $"projects-rows-facts-missing-{width}"), prefix: string.Empty);
    });

    [Theory]
    [InlineData(1280, 800)]
    [InlineData(1000, 700)]
    public Task The_bar_fits_the_window_with_Group_and_Add_project_on_screen(int width, int height) => OnUi(async () =>
    {
        var (window, _) = await OnProjects(width, height);

        foreach (var name in _barButtons)
        {
            var right = Right(Named<Button>(window, name), window);
            Assert.True(right <= window.Bounds.Width, string.Create(CultureInfo.InvariantCulture, $"{name} ends at {right}, past the window's {window.Bounds.Width}"));
        }

        Assert.True(Right(Named<TextBox>(window, "ProjectsSearch"), window) <= Left(Named<Button>(window, "ProjectsSort"), window), "the search box runs under Sort");
    });

    // Issue 4153: at 1000 px the bar drops the words Sort and Group (the values and the buttons' names stay) so the search box keeps room; at 1280 the words show.
    [Theory]
    [InlineData(1000, 700, false)]
    [InlineData(1280, 800, true)]
    public Task A_narrow_bar_drops_the_Sort_and_Group_words_and_the_search_box_keeps_160_px(int width, int height, bool wordsShown) => OnUi(async () =>
    {
        var (window, shell) = await OnProjects(width, height);

        foreach (var (button, word) in new[] { ("ProjectsSort", shell.Projects.SortLabel), ("ProjectsGroup", shell.Projects.GroupLabel) })
        {
            var label = Named<Button>(window, button).GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == word);
            Assert.Equal(wordsShown, label.IsEffectivelyVisible);
        }

        Assert.Equal((shell.Projects.SortName, shell.Projects.GroupName), (Avalonia.Automation.AutomationProperties.GetName(Named<Button>(window, "ProjectsSort")), Avalonia.Automation.AutomationProperties.GetName(Named<Button>(window, "ProjectsGroup"))));   // the names keep saying Sort and Group
        if (!wordsShown)
        {
            Assert.True(Named<TextBox>(window, "ProjectsSearch").Bounds.Width >= 160, string.Create(CultureInfo.InvariantCulture, $"the search box is {Named<TextBox>(window, "ProjectsSearch").Bounds.Width} px wide"));
        }
    });

    // Issue 4158: at 1000 px the Project cell is about 330 px; the reason beside a short name must trim with an ellipsis inside it, and a long name keeps room.
    [Fact]
    public Task At_1000_the_health_reason_trims_inside_the_Project_cell_and_the_name_keeps_room() => OnUi(async () =>
    {
        var (window, _) = await OnProjects(1000, 700);
        var rows = window.GetVisualDescendants().OfType<ProjectRow>().Where(r => r.IsEffectivelyVisible).ToList();

        var kitchen = rows.First(r => Named<TextBlock>(r, "ProjectRowNameText").Text == "Kitchen");
        var reason = Named<TextBlock>(kitchen, "ProjectRowReason");
        var prs = Named<Border>(kitchen, "ProjectRowPrs");
        Assert.True(Right(reason, kitchen) <= Left(prs, kitchen) + 0.5, string.Create(CultureInfo.InvariantCulture, $"the reason ends at {Right(reason, kitchen)}, past the PRs column at {Left(prs, kitchen)}"));
        Assert.True(reason.TextLayout.TextLines.Any(l => l.HasCollapsed), "the reason is cut without an ellipsis");

        var long_ = rows.First(r => Named<TextBlock>(r, "ProjectRowNameText").Text == LongName);
        Assert.True(Named<Border>(long_, "ProjectRowName").Bounds.Width >= 120, "a long name has no room left");
    });

    // Issue 4241: under 1100 px the search placeholder is the short "Search projects" and it fits the box; from 1100 it is the long text. Both menu buttons carry a tooltip with their word and value.
    [Theory]
    [InlineData(1000, 700, "Search projects")]
    [InlineData(1280, 800, "Search name, note, lead")]
    public Task The_search_placeholder_follows_the_width_and_never_trims(int width, int height, string placeholder) => OnUi(async () =>
    {
        var (window, _) = await OnProjects(width, height);
        var box = Named<TextBox>(window, "ProjectsSearch");

        Assert.Equal(placeholder, box.PlaceholderText);
        var mark = box.GetVisualDescendants().OfType<TextBlock>().First(t => string.Equals(t.Text, box.PlaceholderText, StringComparison.Ordinal));
        var needs = new FormattedText(placeholder, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(mark.FontFamily, mark.FontStyle, mark.FontWeight), mark.FontSize, Brushes.White).Width;
        Assert.True(needs <= box.Bounds.Width - box.Padding.Left - box.Padding.Right, string.Create(CultureInfo.InvariantCulture, $"the placeholder needs {needs} px, the box has {box.Bounds.Width - box.Padding.Left - box.Padding.Right}"));
    });

    [Fact]
    public Task The_Sort_and_Group_buttons_carry_a_tooltip_with_their_word_and_value() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects(1000, 700);

        Assert.Equal((shell.Projects.SortName, shell.Projects.GroupName), (ToolTip.GetTip(Named<Button>(window, "ProjectsSort")) as string, ToolTip.GetTip(Named<Button>(window, "ProjectsGroup")) as string));
    });
}
