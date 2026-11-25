using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>Save step in the shell: the shell's toast says "Step saved." with an Undo, and Ctrl+Z (not in a text field) puts the previous graph back as a new revision.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellStepSavedToastTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.True(condition());
    }

    private async Task<ShellViewModel> SavedStep()
    {
        _records.Write(A.Record());
        _server.WorkflowBlocksAnswer = RouteAnswer.Api("workflows-blocks", "listed");
        _server.WorkflowAnswers[Slug] = RouteAnswer.Api("workflows-slug", "found");
        _server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Until(() => shell.State == ShellState.Normal);
        shell.GoTo(PageId.Workflows);
        await Until(() => shell.Workflows.Rows.Count == 2);
        await shell.Workflows.OpenWorkflow(Slug, Token);
        Assert.True(shell.Workflows.Runs.Graph!.SelectNode("agent"));
        await shell.Workflows.OpenSettings(Token);
        var newest = Golden("workflows-slug", "found");
        (newest["revision"], newest["latestRevision"]) = (3, 3);   // the found golden is at revision 2: the save makes 3
        _server.WorkflowNewestAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, newest.ToJsonString());
        shell.Workflows.Editor.Fields.Single(static f => string.Equals(f.Name, "prompt", StringComparison.Ordinal)).Text = "Look twice";
        await shell.Workflows.Save(Token);
        return shell;
    }

    [Fact]
    public Task Saving_a_step_shows_the_toast_with_an_Undo() => UiThread.Run(async () =>
    {
        var shell = await SavedStep();

        Assert.Equal((true, "Step saved.", true), (shell.Toast.IsShown, shell.Toast.Text, shell.Toast.HasUndo));
        Assert.Equal("Undo (Ctrl+Z)", shell.Toast.UndoLabel);
    }, Token);

    [Fact]
    public Task Ctrl_Z_puts_the_previous_graph_back_and_in_a_text_field_does_nothing() => UiThread.Run(async () =>
    {
        var shell = await SavedStep();

        Assert.False(shell.Toast.PressUndoKey(inTextField: true));
        Assert.Empty(_server.ReplacedWorkflows);
        Assert.True(shell.Toast.PressUndoKey(inTextField: false));

        await Until(() => _server.ReplacedWorkflows.Count == 1);
        Assert.Equal(3, _server.ReplacedWorkflows[0].BaseRevision);
        Assert.False(shell.Toast.IsShown);   // the toast's window ends with the Undo
    }, Token);

    [Fact]
    public Task Ctrl_Z_with_no_toast_does_nothing() => UiThread.Run(async () =>
    {
        var shell = await SavedStep();
        Assert.True(shell.Toast.PressUndoKey(inTextField: false));   // the toast's Undo
        await Until(() => _server.ReplacedWorkflows.Count == 1);
        Assert.False(shell.Toast.IsShown);

        Assert.False(shell.Toast.PressUndoKey(inTextField: false));   // no toast now: the key is not spent and nothing is sent

        _ = Assert.Single(_server.ReplacedWorkflows);
    }, Token);

    [Fact]
    public Task Ctrl_Z_on_the_Workflows_page_before_any_save_does_nothing() => UiThread.Run(async () =>
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Until(() => shell.State == ShellState.Normal);
        shell.GoTo(PageId.Workflows);

        Assert.False(shell.Toast.PressUndoKey(inTextField: false));

        Assert.Empty(_server.ReplacedWorkflows);
    }, Token);

    // The button binds IsUndoEnabled: it only repaints when the toast says that property changed, so a newer edit must say so at once, with no 1 s tick of the clock (this one never advances).
    private static List<string> Watch(ShellViewModel shell)
    {
        var changed = new List<string>();
        shell.Toast.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);
        return changed;
    }

    [Fact]
    public Task A_canvas_edit_after_the_save_turns_the_toasts_Undo_off_at_once() => UiThread.Run(async () =>
    {
        var shell = await SavedStep();
        var changed = Watch(shell);
        var newest = Golden("workflows-slug", "found");
        (newest["revision"], newest["latestRevision"]) = (4, 4);   // the canvas edit makes revision 4
        _server.WorkflowNewestAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, newest.ToJsonString());
        var applied = Golden("workflows-slug-ops", "ops-applied");
        applied["revision"] = 4;
        _server.ApplyWorkflowOpsAnswer = new RouteAnswer(HttpStatusCode.OK, applied.ToJsonString());

        await shell.Workflows.Editor.RemoveNode("agent", Token);

        Assert.False(shell.Toast.IsUndoEnabled);
        Assert.Contains("IsUndoEnabled", changed, StringComparer.Ordinal);   // the button repaints now, not at the next tick
    }, Token);

    [Fact]
    public Task An_Activate_on_the_way_turns_the_toasts_Undo_off_at_once() => UiThread.Run(async () =>
    {
        var shell = await SavedStep();
        var changed = Watch(shell);
        _server.WorkflowStateGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var activating = shell.Workflows.Activate(Token);

        Assert.False(shell.Toast.IsUndoEnabled);
        Assert.Contains("IsUndoEnabled", changed, StringComparer.Ordinal);
        changed.Clear();
        _server.WorkflowStateGate.SetResult();
        await activating;
        Assert.True(shell.Toast.IsUndoEnabled);   // and on again when it is done
        Assert.Contains("IsUndoEnabled", changed, StringComparer.Ordinal);
    }, Token);
}
