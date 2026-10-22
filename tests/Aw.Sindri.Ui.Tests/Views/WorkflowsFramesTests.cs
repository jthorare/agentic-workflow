using System.Net;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>U8 key frames (UI spec §18.2: one per state, integration lane) for the Workflows page, against the in-memory API serving the list goldens. Saved under frames/ as u8-workflows-*.png.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsFramesTests : FrameHost
{
    private const string Prefix = "";

    private async Task<(Window Window, ShellViewModel Shell)> OnWorkflows(Func<ShellViewModel, bool> ready)
    {
        var (window, shell) = await Connected(ready);
        shell.GoTo(PageId.Workflows);
        shell.Workflows.SetViewMode(WorkflowViewMode.List);   // these frames are the grouped list; the board has WorkflowsBoardFramesTests
        Settle(window);
        return (window, shell);
    }

    private static List<Border> Rows(Window window) =>
        [.. window.GetVisualDescendants().OfType<Border>().Where(b => string.Equals(b.Name, "WorkflowRowFrame", StringComparison.Ordinal) && b.IsEffectivelyVisible)];

    private static string Address(Border frame) => ((InstanceRowViewModel)frame.DataContext!).Address;

    [Fact]
    public Task u8_workflows_open() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(s => s.Workflows.Groups.Count > 0);

        Expect.Sequence(shell.Workflows.Groups.SelectMany(g => g.Rows).Select(r => r.Address), Rows(window).Select(Address));
        Assert.Equal(shell.Workflows.Head, Named<TextBlock>(window, "WorkflowsHead").Text);
        var locks = window.GetVisualDescendants().OfType<Ui.Views.Components.ProtectedActionButton>().Where(b => b.Name == "WorkflowProtected" && b.IsEffectivelyVisible).ToList();
        var locked = FixtureInstances.List("open").Instances.Single(r => r.Answer is not null && string.Equals(r.Answer.Class, "protected", StringComparison.Ordinal));
        Assert.Equal(locked.Answer!.Cli, ((ViewModels.Components.ProtectedActionViewModel)Assert.Single(locks).DataContext!).Cli);   // the one lock sits on the Locked row
        Assert.False(Visible(window, "WorkflowsLegend"));   // the key sits behind its toggle until asked for
        shell.Workflows.ToggleLegend();
        Settle(window);
        Assert.Equal((true, shell.Workflows.LegendText), (Visible(window, "WorkflowsLegend"), Named<TextBlock>(window, "WorkflowsLegend").Text));
        Assert.Equal(shell.Workflows.Groups.Count, window.GetVisualDescendants().OfType<Button>().Count(t => t.Name == "WorkflowGroupToggle"));
        var recipeButtons = window.GetVisualDescendants().OfType<Button>().Where(b => b.Name == "WorkflowGroupRecipe" && b.IsEffectivelyVisible).ToList();
        Expect.Sequence(shell.Workflows.Groups.Select(g => g.RecipeLinkName), recipeButtons.Select(Heard.Name));   // reached and named, by the peer
        Assert.All(recipeButtons, b => Assert.StartsWith(b.Content!.ToString()![..^2], Heard.Name(b), StringComparison.Ordinal));
        PrimaryOnly(window, shell);
        AssistiveNames(window, shell);
        Frame(window, "u8-workflows-open", Prefix);
    });

    // The row is six columns: the item with its mono reference, the stage, the one status, the step, who it waits on and when it changed. The workflow's
    // name is on the group row above; the inputs and every stage wait in the run's drawer.
    private static void PrimaryOnly(Window window, ShellViewModel shell)
    {
        var row = shell.Workflows.Groups[0].Rows[0];
        var frame = window.GetVisualDescendants().OfType<Border>().First(b => string.Equals(b.Name, "WorkflowRowFrame", StringComparison.Ordinal) && ReferenceEquals(b.DataContext, row));
        var texts = frame.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? string.Empty).ToList();
        Assert.Contains(texts, t => string.Equals(t, row.Subject, StringComparison.Ordinal));
        Assert.Contains(texts, t => string.Equals(t, row.Reference, StringComparison.Ordinal));
        Assert.Contains(texts, t => string.Equals(t, row.StageName, StringComparison.Ordinal));
        Assert.Contains(texts, t => string.Equals(t, row.UpdatedText, StringComparison.Ordinal));
        Assert.Contains(frame.GetVisualDescendants().OfType<Ui.Views.Controls.StatusBadge>(), b => b.IsEffectivelyVisible && b.Status == row.Status);
        Assert.DoesNotContain(texts, t => t.Contains("workflow.", StringComparison.Ordinal));
        Assert.DoesNotContain(frame.GetVisualDescendants().OfType<Border>(), b => (string.Equals(b.Name, "WorkflowRowInputs", StringComparison.Ordinal) || string.Equals(b.Name, "WorkflowRowStages", StringComparison.Ordinal)) && b.IsEffectivelyVisible);
        var head = Named<Border>(window, "WorkflowColumnHead");   // the head sits above the scrolled rows
        var kept = ListColumns.For(Named<Border>(window, "WorkflowsPanel").Bounds.Width);
        string[] heads = [Labels.WorkflowsColItem, Labels.WorkflowsColStage, Labels.WorkflowsColStatus, Labels.WorkflowsColStep, Labels.WorkflowsColWaitingOn, Labels.WorkflowsColUpdated];
        Expect.Sequence(heads.Where((_, i) => kept.Shows(i)), head.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? string.Empty));
        Assert.DoesNotContain(head.GetVisualAncestors().OfType<ScrollViewer>(), s => s.Name == "WorkflowsListScroll");
    }

    // What assistive tech hears, from each control's automation peer (a TextBlock's peer ignores a Name, so these sit on wrappers): review run 2, W5 and W6.
    private static void AssistiveNames(Window window, ShellViewModel shell)
    {
        Assert.Equal(WorkflowsLegend.Name, Heard.Wrapper(Assert.Single(window.GetVisualDescendants().OfType<Border>(), b => b.Name == "WorkflowsLegendName" && b.IsEffectivelyVisible)));
        var row = shell.Workflows.Groups[0].Rows[0];
        var title = window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "WorkflowRowOpen" && b.IsEffectivelyVisible && ReferenceEquals(b.DataContext, row));
        Assert.Equal(row.OpenName, Heard.Name(title));
        Assert.Contains(row.Subject, Heard.Name(title), StringComparison.Ordinal);
        var frame = window.GetVisualDescendants().OfType<Border>().First(b => string.Equals(b.Name, "WorkflowRowFrame", StringComparison.Ordinal) && ReferenceEquals(b.DataContext, row));
        Assert.Equal(row.AccessibleName, Heard.Name(frame));
    }

    private async Task ListAt(double width, string frame)
    {
        var (window, shell) = await Connected(s => s.Workflows.Groups.Count > 0, width, 900);
        shell.GoTo(PageId.Workflows);
        shell.Workflows.SetViewMode(WorkflowViewMode.List);
        Settle(window);

        var rows = Rows(window);
        Expect.Sequence(shell.Workflows.Groups.SelectMany(g => g.Rows).Select(r => r.Address), rows.Select(Address));
        var kept = ListColumns.For(Named<Border>(window, "WorkflowsPanel").Bounds.Width);
        var stages = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "WorkflowRowStage" && t.IsEffectivelyVisible).ToList();
        Assert.Equal(kept.Shows(ListColumns.Stage), stages.Count > 0);   // 1440 keeps all six columns; at 900 Stage and Waiting on are gone
        Assert.Equal(kept.Shows(ListColumns.WaitingOn), window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Name == "WorkflowRowWho" && t.IsEffectivelyVisible));
        Assert.True(Named<Border>(window, "WorkflowsPanel").Bounds.Width <= width);   // the page never scrolls sideways
        Frame(window, "workflows-list-" + frame, "f3-");
    }

    [Fact]
    public Task The_list_draws_six_columns_at_1440() => OnUi(() => ListAt(1440, "1440"));

    [Fact]
    public Task The_list_drops_Stage_and_Waiting_on_at_900() => OnUi(() => ListAt(900, "900"));

    [Fact]
    public Task u8_workflows_recipe_filter() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(s => s.Workflows.Groups.Count > 0);
        var recipe = (string)GoldenFiles.Node("workflow-instances", "recipe-filter")["instances"]![0]!["recipe"]!["id"]!;

        await shell.Workflows.FilterRecipe(recipe, Token);
        await Until(window, () => Rows(window).Count > 0 && Rows(window).TrueForAll(b => ((InstanceRowViewModel)b.DataContext!).Recipe == recipe));

        Assert.Equal(shell.Workflows.Head, Named<TextBlock>(window, "WorkflowsHead").Text);
        Frame(window, "u8-workflows-recipe-filter", Prefix);
    });

    [Fact]
    public Task u8_workflows_empty() => OnUi(async () =>
    {
        Server.WorkflowAnswer = RouteAnswer.Api("workflow-instances", "empty");
        var (window, shell) = await OnWorkflows(s => s.Workflows.IsEmpty);

        Assert.Equal((true, (string)GoldenFiles.Node("workflow-instances", "empty")["empty"]!), (Visible(window, "WorkflowsPageEmpty"), Named<TextBlock>(window, "WorkflowsPageEmpty").Text));
        Assert.Empty(Rows(window));
        Assert.False(Visible(window, "WorkflowsDoneToggle"));
        Frame(window, "u8-workflows-empty", Prefix);
    });

    [Fact]
    public Task u8_workflows_stalled() => OnUi(async () =>
    {
        Server.ServiceView = RouteAnswer.SpecShaped("services", "stalled");
        var (window, shell) = await OnWorkflows(s => s.Health.Banner.IsStalled && s.Workflows.Groups.Count > 0);

        Assert.True(Visible(window, "WorkflowsPageStale"));
        Assert.NotEmpty(Rows(window));
        Assert.All(shell.Workflows.Groups.SelectMany(g => g.Rows), r => Assert.True(r.IsStale));
        Frame(window, "u8-workflows-stalled", Prefix);
    });

    [Fact]
    public Task u8_workflows_unreadable() => OnUi(async () =>
    {
        var body = (JsonObject)GoldenFiles.Node("workflow-instances", "open");
        _ = body.Remove("counts");
        Server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
        var (window, _) = await OnWorkflows(s => s.Workflows.IsUnreadable);

        Assert.Equal((true, Labels.StripUnreadable), (Visible(window, "WorkflowsPageUnreadable"), Named<TextBlock>(window, "WorkflowsPageUnreadable").Text));
        Assert.Empty(Rows(window));
        Frame(window, "u8-workflows-unreadable", Prefix);
    });
}
