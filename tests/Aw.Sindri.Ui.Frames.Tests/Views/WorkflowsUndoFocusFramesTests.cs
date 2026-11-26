using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// After an undo (Ctrl+Z or the toast's button) the reload rebuilds the step sheet's fields, which drops the focused control; focus must land back where the person was: the sheet's
/// prompt field while the sheet is open, the graph while it is shut. It must never fall to the page, which draws a focus ring around everything (qa on #4385).
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsUndoFocusFramesTests : FrameHost
{
    private async Task<(Window Window, ShellViewModel Shell)> SavedStep()
    {
        Server.WorkflowBlocksAnswer = RouteAnswer.Api("workflows-blocks", "listed");
        Server.WorkflowAnswers[Slug] = RouteAnswer.Api("workflows-slug", "found");
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        var (window, shell) = await Connected(width: 1280, height: 1000);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Graph is not null, "the graph never showed");
        Assert.True(shell.Workflows.Runs.Graph!.SelectNode("agent"));
        await shell.Workflows.OpenSettings(CancellationToken.None);
        await Until(window, () => shell.Workflows.Editor.Fields.Count > 0 && Visible(window, "EditorFields"), "the editor never showed the step's fields");
        var newest = Golden("workflows-slug", "found");
        (newest["revision"], newest["latestRevision"]) = (3, 3);   // the found golden is at revision 2: the save makes 3
        Server.WorkflowNewestAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, newest.ToJsonString());
        var applied = Golden("workflows-slug-ops", "ops-applied");
        applied["revision"] = 3;
        Server.ApplyWorkflowOpsAnswer = new RouteAnswer(HttpStatusCode.OK, applied.ToJsonString());
        shell.Workflows.Editor.Fields.Single(static f => string.Equals(f.Name, "prompt", StringComparison.Ordinal)).Text = "Look twice";
        await shell.Workflows.Save(CancellationToken.None);
        await Until(window, () => shell.Toast.IsShown && shell.Toast.CanUndo, "the Step saved toast never showed with an Undo");
        return (window, shell);
    }

    private static bool Inside(Window window, string name) =>
        window.FocusManager?.GetFocusedElement() is Visual focused && Named<Control>(window, name) is { } region && (ReferenceEquals(focused, region) || focused.GetVisualAncestors().Contains(region));

    [Fact]
    public Task After_an_undo_with_the_sheet_open_focus_is_back_in_the_sheet() => OnUi(async () =>
    {
        var (window, shell) = await SavedStep();
        var prompt = Named<Control>(window, "EditorSheet").GetVisualDescendants().OfType<TextBox>().First(static t => t.IsEffectivelyVisible && t.DataContext is ViewModels.Workflows.ConfigFieldViewModel f && string.Equals(f.Name, "prompt", StringComparison.Ordinal));
        Assert.True(prompt.Focus(NavigationMethod.Tab));

        Assert.True(shell.Toast.PressUndoKey(inTextField: false));
        await Until(window, () => Server.ReplacedWorkflows.Count == 1, "the undo never sent its PUT");

        await Until(window, () => Inside(window, "EditorSheet"), "focus never came back to the sheet after the undo");
    });

    [Fact]
    public Task After_an_undo_with_the_sheet_shut_focus_is_back_on_the_graph() => OnUi(async () =>
    {
        var (window, shell) = await SavedStep();
        shell.Workflows.CloseSettings();
        await Until(window, () => !Visible(window, "EditorSheet"), "the sheet never closed");
        var graph = Named<Control>(window, "RunsCanvas").GetVisualDescendants().OfType<Control>().First(static c => c.Focusable && c.IsEffectivelyVisible);
        Assert.True(graph.Focus(NavigationMethod.Tab));

        Assert.True(shell.Toast.PressUndoKey(inTextField: false));
        await Until(window, () => Server.ReplacedWorkflows.Count == 1, "the undo never sent its PUT");

        await Until(window, () => Inside(window, "RunsCanvas"), "focus never came back to the graph after the undo");
    });
}
