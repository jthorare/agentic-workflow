using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views.Controls;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>Key frames for the read-only Workflows page (#2258 PR 17 part 1) at 1440 and 900 px wide, against the in-memory API serving the workflow goldens. Saved under frames/ as workflows-*.png.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsFramesTests : FrameHost
{
    private const string Prefix = "workflows-";

    private async Task<(Window Window, ShellViewModel Shell)> OnWorkflows(double width)
    {
        Server.WorkflowAnswers[Slug] = RouteAnswer.Api("workflows-slug", "found");
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        Server.WorkflowRunAnswers[RunId] = RouteAnswer.Api("workflow-runs-run", "finished");
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        return (window, shell);
    }

    // A theme token's colour in one variant, so a test can compare what a chip drew with what each theme says.
    private static Color ThemeColor(Window window, string key, ThemeVariant variant)
    {
        Assert.True(window.TryFindResource(key, variant, out var value), $"{key} is missing in {variant}");
        return Assert.IsType<ISolidColorBrush>(value, exactMatch: false).Color;
    }

    private static Color ChipColor(TextBlock chip) => Assert.IsType<ISolidColorBrush>(chip.Foreground, exactMatch: false).Color;

    private static Control? Focused(Window window) => window.FocusManager?.GetFocusedElement() as Control;

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task The_list_of_workflows(double width) => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(width);

        Assert.True(Visible(window, "WorkflowRows"));
        Assert.Equal(shell.Workflows.Head, Named<TextBlock>(window, "WorkflowsHead").Text);
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"list-{width}"), Prefix);
    });

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task The_empty_state_offers_New_workflow_and_names_the_agent_command(double width) => OnUi(async () =>
    {
        Server.WorkflowsAnswer = RouteAnswer.Api("workflows", "list-empty");
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.IsEmpty, "the Workflows page never read the empty list");

        Assert.False(Visible(window, "WorkflowsHead"));
        var button = Named<Button>(window, "WorkflowsNew");
        Assert.True(button.IsEffectivelyVisible);
        Assert.True(button.IsEnabled);
        Assert.False(Visible(window, "WorkflowsCreateForm"));
        Assert.Equal("sindri wf create", Named<TextBlock>(window, "WorkflowsAgentCommand").Text);
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"empty-{width}"), Prefix);
    });

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task The_New_workflow_form_asks_for_a_name_and_a_template_and_says_why_Create_is_off(double width) => OnUi(async () =>
    {
        Server.WorkflowsAnswer = RouteAnswer.Api("workflows", "list-empty");
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.IsEmpty, "the Workflows page never read the empty list");

        shell.Workflows.OpenCreate();
        await Until(window, () => Visible(window, "WorkflowsCreateForm"), "the New workflow form never showed");

        Assert.False(Visible(window, "WorkflowsNew"));
        Assert.True(Visible(window, "WorkflowsNewName"));
        Assert.True(Visible(window, "WorkflowsNewTemplate"));
        Assert.False(Named<Button>(window, "WorkflowsCreate").IsEnabled);
        Assert.True(Visible(window, "WorkflowsNewReason"));
        Assert.False(Visible(window, "WorkflowsEmptyName"));   // #2688: the empty sentence and the agent route hide while the form is open
        Assert.False(Visible(window, "WorkflowsAgentCommand"));
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"empty-form-{width}"), Prefix);
    });

    // qa round 2: a server refusal shows inside the form as a red sentence with no code; the code is the line's tooltip.
    [Fact]
    public Task A_refused_create_shows_a_red_sentence_in_the_form_and_the_code_in_its_tooltip() => OnUi(async () =>
    {
        Server.WorkflowsAnswer = RouteAnswer.Api("workflows", "list-empty");
        Server.CreateWorkflowAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.Conflict, "SND-GRAPH-009", "The short id 'morning-check' is taken by another workflow. Pick another.");
        var (window, shell) = await Connected(width: 1280, height: 900);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.IsEmpty, "the Workflows page never read the empty list");
        shell.Workflows.OpenCreate();
        shell.Workflows.NewName = "Morning check";
        await shell.Workflows.Create(CancellationToken.None);
        await Until(window, () => Visible(window, "WorkflowsCreateError"), "the refusal line never showed");

        var form = Named<Control>(window, "WorkflowsCreateForm");
        var line = Named<Border>(form, "WorkflowsCreateError");
        var text = line.GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.True(line.IsEffectivelyVisible, "the refusal line is not visible inside the form");
        Assert.DoesNotContain("SND-", text.Text, StringComparison.Ordinal);
        Assert.Equal("SND-GRAPH-009", ToolTip.GetTip(line));
        Assert.Equal(ThemeColor(window, "Brush.Error", window.ActualThemeVariant), ChipColor(text));
        Frame(window, "create-refused-1280", Prefix);
    });

    // qa (#2719): a server refusal left an empty laid-out line above its red sentence (37 px below the template text against 13 px for the local taken-id line). Both red sentences start at the same y,
    // with nothing empty between them and the template description.
    [Theory]
    [InlineData(1000)]
    [InlineData(1280)]
    public Task A_server_refusal_and_the_local_taken_id_line_start_at_the_same_y(double width) => OnUi(async () =>
    {
        Server.WorkflowsAnswer = RouteAnswer.Api("workflows", "list-two");   // holds morning-check, so the form says it before Create
        Server.CreateWorkflowAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.Conflict, "SND-GRAPH-009", "the slug 'evening-check' belongs to another workflow; pick another");
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        shell.Workflows.OpenCreate();
        shell.Workflows.NewName = "Morning check";
        await Until(window, () => Visible(window, "WorkflowsNewReason"), "the local taken-id line never showed");
        Settle(window);
        var local = RedLineTop(window);

        shell.Workflows.NewName = "Evening check";
        await shell.Workflows.Create(CancellationToken.None);
        await Until(window, () => Visible(window, "WorkflowsCreateError"), "the server refusal never showed");
        Settle(window);
        var server = RedLineTop(window);

        Assert.True(Math.Abs(local - server) < 0.5, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the local line starts at y={local} and the server refusal at y={server}"));
    });

    // The top of the form's red sentence (the visible one of the local reason and the server refusal), measured inside the form.
    private static double RedLineTop(Window window)
    {
        var form = Named<Control>(window, "WorkflowsCreateForm");
        var text = form.GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("invalid") && t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text));
        return text.TranslatePoint(default, form)!.Value.Y;   // why: the text is inside the form
    }

    [Fact]
    public Task A_row_title_does_not_hug_the_rows_top_border_and_New_workflow_sits_in_the_header_beside_the_list() => OnUi(async () =>
    {
        var (window, _) = await OnWorkflows(1280);
        Settle(window);

        var header = Named<Button>(window, "WorkflowsNewHeader");
        Assert.True(header.IsEffectivelyVisible && header.IsEnabled);
        var list = Named<ItemsControl>(window, "WorkflowRows");
        var row = list.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("item-row"));
        var title = row.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("row-title"));
        Assert.True(title.TranslatePoint(default, row)!.Value.Y >= 8, "the title is at least 8 px below the row's top");
        Assert.False(title.Focusable, "the row draws the focus ring, not the title");
    });

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task A_workflows_runs(double width) => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(width);

        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Rows.Count > 0, "the runs never showed");

        Assert.True(Visible(window, "RunRows"));
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"runs-{width}"), Prefix);
    });

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task A_runs_detail(double width) => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(width);

        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await shell.Workflows.OpenRun(RunId, CancellationToken.None);
        await Until(window, () => shell.Workflows.Run.Groups.Count > 0, "the run never showed");

        Assert.True(Visible(window, "RunGroups"));
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"run-{width}"), Prefix);
    });

    // The light theme: the runs (status chips in the ok and error tones, Run now) and a running run's detail (Cancel run), each beside its dark frame above.
    [Fact]
    public Task A_workflows_runs_in_the_light_theme() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(1440);
        window.RequestedThemeVariant = ThemeVariant.Light;

        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Rows.Count > 0, "the runs never showed");

        Assert.True(Visible(window, "RunNow"));
        await Until(window, () => window.ActualThemeVariant == ThemeVariant.Light, "the window never took the light theme");
        var chip = window.GetVisualDescendants().OfType<StateChipView>().First(c => c.IsEffectivelyVisible && c.Tone == "ok").GetVisualDescendants().OfType<TextBlock>().First();
        Assert.Equal(ThemeColor(window, "Brush.Success", ThemeVariant.Light), ChipColor(chip));   // the theme token (Success is one colour in both variants, Theme/Tokens.axaml)
        Frame(window, "runs-light-1440", Prefix);
    });

    [Fact]
    public Task A_running_runs_detail_in_the_light_theme() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(1440);
        Server.WorkflowRunAnswers[RunId] = RouteAnswer.Api("workflow-runs-run", "running");   // after OnWorkflows, which serves the finished run
        window.RequestedThemeVariant = ThemeVariant.Light;

        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await shell.Workflows.OpenRun(RunId, CancellationToken.None);
        await Until(window, () => shell.Workflows.Run.Groups.Count > 0, "the run never showed");

        Assert.True(Visible(window, "RunCancel"));
        await Until(window, () => window.ActualThemeVariant == ThemeVariant.Light, "the window never took the light theme");
        var chip = Named<StateChipView>(window, "RunChipName").GetVisualDescendants().OfType<TextBlock>().First();   // a running run: the ok tone
        Assert.Equal(ThemeColor(window, "Brush.Success", ThemeVariant.Light), ChipColor(chip));
        Frame(window, "run-light-1440", Prefix);
    });

    // #2807: the runs level binds the Activate and Pause commands the view model already has.
    [Fact]
    public Task Activate_and_Pause_buttons_are_bound_on_the_runs_level() => OnUi(async () =>
    {
        const string off = "weekly-report";
        var (window, shell) = await OnWorkflows(1280);
        Server.WorkflowRunsAnswers[off] = RouteAnswer.Api("workflows-slug-runs", "list-three");

        await shell.Workflows.OpenWorkflow(off, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Rows.Count > 0, "the runs never showed");
        var activate = Named<Button>(window, "WorkflowActivate");
        Assert.True(activate.IsEffectivelyVisible, "Activate is not visible on an off workflow");
        Assert.False(Visible(window, "WorkflowPause"));
        Click(activate);
        await Until(window, () => Server.ActivatedWorkflows.Count > 0, "Activate never sent its call");
        Assert.Equal([off], Server.ActivatedWorkflows);

        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.CanPause, "the active workflow never offered Pause");
        var pause = Named<Button>(window, "WorkflowPause");
        Assert.True(pause.IsEffectivelyVisible, "Pause is not visible on an active workflow");
        Click(pause);
        await Until(window, () => Server.PausedWorkflows.Count > 0, "Pause never sent its call");
        Assert.Equal([Slug], Server.PausedWorkflows);
    });

    // A level opens on its Back button, and Back lands on the first row of the level it returns to, never on the hidden Back button.
    [Fact]
    public Task Focus_goes_to_Back_when_a_level_opens_and_to_the_first_row_when_Back_returns() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(1440);

        var title = Named<ItemsControl>(window, "WorkflowRows").GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible);
        Click(title);
        await Until(window, () => shell.Workflows.Runs.Rows.Count > 0 && Focused(window)?.Name == "WorkflowsBack", "Back never took focus when the runs opened");

        var back = Named<Button>(window, "WorkflowsBack");
        Click(back);
        await Until(window, () => shell.Workflows.IsList && Focused(window) is Border { DataContext: WorkflowRowViewModel } focused && focused.Classes.Contains("item-row"), "the first workflow never took focus when Back returned");

        var first = Assert.IsType<Border>(Focused(window));   // the row is the focus stop since #2550, not its title button
        Assert.Equal(shell.Workflows.Rows[0], first.DataContext);
        Assert.False(back.IsEffectivelyVisible);
    });

    // #3233 PR 2: every status chip on the page is a StateChipView in the status-chip tone set, never a hand-drawn Border.status-chip.
    private static string ToneWord(ChipTone tone) => tone switch
    {
        ChipTone.Ok => "ok",
        ChipTone.Attn => "attention",
        ChipTone.Fail => "error",
        ChipTone.Neutral => "neutral",
        _ => "neutral",
    };

    private static void AssertChip(StateChipView chip, string word, string glyph, string tone, string what)
    {
        Assert.Equal(tone, chip.Tone);
        Assert.Contains("status-" + tone, chip.Classes, StringComparer.Ordinal);
        Assert.False(string.IsNullOrEmpty(chip.Word), $"{what}: the chip has no word");
        Assert.Equal(word, chip.Word);
        Assert.Equal(glyph, chip.Glyph);
        Assert.False(string.IsNullOrEmpty(word), $"{what}: the view model has no chip word");
    }

    private static void AssertNoHandDrawnChip(Window window) =>
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), b => b is not StateChipView && b.Classes.Contains("status-chip"));

    [Fact]
    public Task The_workflow_rows_draw_their_state_as_StateChipViews() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(1440);

        AssertNoHandDrawnChip(window);
        var chips = window.GetVisualDescendants().OfType<StateChipView>().Where(c => c.IsEffectivelyVisible).ToList();
        Assert.Equal(shell.Workflows.Rows.Count, chips.Count);
        for (var i = 0; i < chips.Count; i++)
        {
            var row = shell.Workflows.Rows[i];
            AssertChip(chips[i], row.ChipWord, row.ChipGlyph, row.IsActive ? "ok" : "neutral", $"workflow {row.Slug}");
            Assert.Equal(chips[i].Tone, row.ChipToneWord);
        }
    });

    [Fact]
    public Task The_run_rows_draw_their_status_as_StateChipViews() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(1440);
        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Rows.Count > 0, "the runs never showed");

        AssertNoHandDrawnChip(window);
        var chips = window.GetVisualDescendants().OfType<StateChipView>().Where(c => c.IsEffectivelyVisible).ToList();
        Assert.Equal(shell.Workflows.Runs.Rows.Count, chips.Count);
        for (var i = 0; i < chips.Count; i++)
        {
            var row = shell.Workflows.Runs.Rows[i];
            AssertChip(chips[i], row.ChipWord, row.ChipGlyph, ToneWord(row.Tone), $"run {row.Id}");
            Assert.Equal(chips[i].Tone, row.ChipToneWord);
        }

        Assert.Contains(chips, c => c.Tone == "ok");
        Assert.Contains(chips, c => c.Tone == "error");   // list-three holds a succeeded and a failed run
    });

    [Fact]
    public Task The_run_header_and_the_node_runs_draw_their_status_as_StateChipViews() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(1440);
        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await shell.Workflows.OpenRun(RunId, CancellationToken.None);
        await Until(window, () => shell.Workflows.Run.Groups.Count > 0, "the run never showed");

        AssertNoHandDrawnChip(window);
        var run = shell.Workflows.Run;
        var header = Assert.IsType<StateChipView>(Named<Control>(window, "RunChipName"));   // #3194 builds on this chip
        AssertChip(header, run.ChipWord, run.ChipGlyph, ToneWord(run.Tone), "the run header");
        Assert.Equal(header.Tone, run.ChipToneWord);

        var nodes = run.Groups.SelectMany(g => g.Runs).ToList();
        var chips = window.GetVisualDescendants().OfType<StateChipView>().Where(c => c.IsEffectivelyVisible && !ReferenceEquals(c, header)).ToList();
        Assert.Equal(nodes.Count, chips.Count);
        for (var i = 0; i < chips.Count; i++)
        {
            AssertChip(chips[i], nodes[i].ChipWord, nodes[i].ChipGlyph, ToneWord(nodes[i].Tone), $"node run {nodes[i].TitleText}");
            Assert.Equal(chips[i].Tone, nodes[i].ChipToneWord);
        }
    });

    // #3233: the issue's own complaint, "a run can't show Running or Waiting". A running run reads "running" and a waiting run "waiting", not ok and attention.
    [Fact]
    public Task A_running_and_a_waiting_run_show_their_own_tones() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(1440);
        const string body = "{\"slug\":\"morning-check\",\"more\":false,\"runs\":["
            + "{\"id\":\"morning-check@1:5\",\"revision\":1,\"status\":\"waiting\",\"trigger\":\"manual\",\"startedAt\":\"2026-01-01T00:05:00.0000000Z\",\"endedAt\":null,\"error\":null},"
            + "{\"id\":\"morning-check@1:4\",\"revision\":1,\"status\":\"running\",\"trigger\":\"manual\",\"startedAt\":\"2026-01-01T00:04:00.0000000Z\",\"endedAt\":null,\"error\":null}]}";
        Server.WorkflowRunsAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, body);
        Server.WorkflowRunAnswers[RunId] = RouteAnswer.Api("workflow-runs-run", "running");

        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Rows.Count == 2, "the two runs never showed");
        var rows = window.GetVisualDescendants().OfType<StateChipView>().Where(c => c.IsEffectivelyVisible).ToList();
        Assert.Equal("waiting running", string.Join(' ', rows.Select(c => c.Tone)));
        Assert.All(rows, c => Assert.Contains("status-" + c.Tone, c.Classes, StringComparer.Ordinal));

        await shell.Workflows.OpenRun(RunId, CancellationToken.None);
        await Until(window, () => shell.Workflows.Run.Groups.Count > 0, "the run never showed");
        var header = Assert.IsType<StateChipView>(Named<Control>(window, "RunChipName"));
        Assert.Equal("running", header.Tone);
    });
}
