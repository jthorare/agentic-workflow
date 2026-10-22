using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The keys on the Workflows page (screens §4.2, U8 T7): J / K across the groups, Enter, A, X, and B and N naming their phase, with the selection kept by address.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsKeysTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IReadOnlyList<InstanceSummary> Served => FixtureInstances.List("open").Instances;

    private static InstanceSummary Desk => Served.First(r => r.Answer is null && r.Actions.Contains("CompleteStep", StringComparer.Ordinal) && r.States.Any(s => s.Desk && string.Equals(s.State, r.State, StringComparison.Ordinal)));

    private static InstanceSummary Locked => Served.First(r => r.Answer is not null && string.Equals(r.Answer.Class, "protected", StringComparison.Ordinal));

    private static InstanceSummary Plain => Served.First(r => r.Answer is null && r.Actions.Contains("ApplyCommand:cancel", StringComparer.Ordinal) && !r.Actions.Contains("CompleteStep", StringComparer.Ordinal));

    private static InstanceDetail Detail(InstanceSummary row) => FixtureInstances.Details.First(d => string.Equals(d.Detail.Id, row.Id, StringComparison.Ordinal)).Detail;

    private async Task<ShellViewModel> OnWorkflows()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.Workflows.Groups.Count > 0, Token);
        shell.GoTo(PageId.Workflows);
        shell.Workflows.SetViewMode(WorkflowViewMode.List);   // the grouped list: the keys walk its groups
        return shell;
    }

    private static List<string> Flat(ShellViewModel shell) => [.. shell.Workflows.Groups.SelectMany(g => g.Rows).Select(r => r.Address)];

    private static bool Reached(ShellViewModel shell) => shell.Instance.Phase == FramePhase.Shown;

    [Fact]
    public Task Enter_opens_the_instance_frame() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        Assert.True(shell.Press("J"));
        var address = Flat(shell)[0];

        Assert.True(shell.Press("Enter"));
        await ShellWait.Until(shell, () => Reached(shell), Token);

        Assert.Equal((address, address, true), (shell.Instance.Address, shell.OpenerWorkflow, shell.IsInstanceSheet));
    }, Token);

    [Fact]
    public Task A_on_a_desk_task_opens_the_frame_on_the_pending_output() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        var asked = new List<(FrameFocus Target, string? Key)>();
        shell.Instance.FocusRequested += (_, e) => asked.Add((e.Target, e.Key));
        var pending = Detail(Desk).Facts.First(f => f.Status == FactStatus.Pending && f.Pending is not null).Key;
        Assert.True(shell.Workflows.Select(Desk.Address));

        Assert.True(shell.Press("A"));
        await ShellWait.Until(shell, () => Reached(shell), Token);

        Assert.Equal(Desk.Address, shell.Instance.Address);
        Assert.Equal([(FrameFocus.PendingOutput, pending)], asked);
    }, Token);

    [Fact]
    public Task A_on_a_protected_answer_opens_the_lock_popover() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        Assert.True(shell.Workflows.Select(Locked.Address));
        var row = shell.Workflows.SelectedRow!;

        Assert.True(shell.Press("A"));

        Assert.Equal((Locked.Answer!.Cli, true, false), (row.Protected?.Cli, row.Protected?.IsOpen, shell.IsSheetOpen));
    }, Token);

    [Fact]
    public Task A_on_a_row_with_nothing_to_answer_does_nothing() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        Assert.True(shell.Workflows.Select(Plain.Address));

        Assert.False(shell.Press("A"));
        Assert.False(shell.IsSheetOpen);
    }, Token);

    [Fact]
    public Task X_shows_the_served_confirm_and_Keep_sends_nothing() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        var served = Detail(Plain).Current.Status.Commands.First(c => c.Verb == StatusVerb.Cancel).Confirm;
        Assert.True(shell.Workflows.Select(Plain.Address));

        Assert.True(shell.Press("X"));
        await ShellWait.Until(shell, () => Reached(shell) && shell.Instance.Expanded is { IsConfirmingCancel: true }, Token);

        var expanded = shell.Instance.Expanded!;
        Assert.Equal(served, expanded.ConfirmLine);
        expanded.Keep();
        Assert.Equal((false, string.Empty), (expanded.IsConfirmingCancel, expanded.PendingNote));
    }, Token);

    [Fact]
    public Task B_says_it_arrives_with_its_phase() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        Assert.True(shell.Workflows.Select(Plain.Address));

        Assert.True(shell.Press("B"));
        Assert.Equal(Labels.HandBackArrives, shell.Workflows.KeyNote);
        Assert.False(shell.IsSheetOpen);

        Assert.True(shell.Press("J"));
        Assert.Equal(string.Empty, shell.Workflows.KeyNote);
    }, Token);

    [Fact]
    public Task J_K_move_across_groups() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        var flat = Flat(shell);
        Assert.True(shell.Workflows.Groups.Count > 1);

        var forward = new List<string?>();
        foreach (var _ in flat)
        {
            Assert.True(shell.Press("J"));
            forward.Add(shell.Workflows.SelectedAddress);
        }

        Expect.Sequence(flat, forward);
        Assert.True(shell.Press("J"));
        Assert.Equal(flat[^1], shell.Workflows.SelectedAddress);

        var back = new List<string?>();
        for (var i = flat.Count - 2; i >= 0; i--)
        {
            Assert.True(shell.Press("K"));
            back.Add(shell.Workflows.SelectedAddress);
        }

        Expect.Sequence(flat.AsEnumerable().Reverse().Skip(1), back);
        Assert.Equal(1, shell.Workflows.Groups.SelectMany(g => g.Rows).Count(r => r.IsSelected));
    }, Token);

    [Fact]
    public Task J_skips_the_rows_of_a_collapsed_group() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        var second = shell.Workflows.Groups[1];
        var first = shell.Workflows.Groups[0].Rows[^1].Address;
        second.Toggle();
        Assert.True(shell.Workflows.Select(first));

        Assert.True(shell.Press("J"));

        Assert.Equal(shell.Workflows.Groups[2].Rows[0].Address, shell.Workflows.SelectedAddress);
    }, Token);

    [Fact]
    public Task The_keys_belong_to_the_page_that_is_showing() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        Assert.True(shell.Workflows.Select(Plain.Address));
        shell.GoTo(PageId.Home);

        _ = shell.Press("Enter");

        Assert.False(shell.IsInstanceSheet);
    }, Token);

    private static List<(bool Live, string Text)> Heard(LiveAnnouncer voice)
    {
        var heard = new List<(bool Live, string Text)>();
        voice.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(voice.Announcement), StringComparison.Ordinal) || string.Equals(e.PropertyName, nameof(voice.IsAnnouncing), StringComparison.Ordinal))
            {
                heard.Add((voice.IsAnnouncing, voice.Announcement));
            }
        };
        return heard;
    }

    [Fact]
    public Task B_then_B_announces_twice_and_J_never_writes_an_empty_text() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        Assert.True(shell.Workflows.Select(Plain.Address));
        var heard = Heard(shell.Workflows.KeyNoteVoice);

        Assert.True(shell.Press("B"));
        Assert.True(shell.Press("B"));
        Assert.Equal(2, heard.Count(h => h.Live && string.Equals(h.Text, Labels.HandBackArrives, StringComparison.Ordinal)));
        Assert.True(shell.Press("J"));

        Assert.DoesNotContain(heard, h => h.Text.Length == 0);
        Assert.Equal((false, string.Empty), (shell.Workflows.KeyNoteVoice.IsAnnouncing, shell.Workflows.KeyNote));
    }, Token);

    [Fact]
    public Task B_with_no_row_selected_still_shows_its_note() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        Assert.Null(shell.Workflows.SelectedAddress);

        Assert.True(shell.Press("B"));

        Assert.Equal(Labels.HandBackArrives, shell.Workflows.KeyNote);
    }, Token);

    [Fact]
    public Task A_note_goes_when_the_selection_changes_but_stays_when_focus_arrives_on_the_selected_row() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        var flat = Flat(shell);
        Assert.True(shell.Workflows.Select(flat[0]));
        Assert.True(shell.Press("B"));

        Assert.True(shell.Workflows.Select(flat[0]));
        Assert.Equal(Labels.HandBackArrives, shell.Workflows.KeyNote);
        Assert.True(shell.Workflows.Select(flat[1]));

        Assert.Equal((string.Empty, false), (shell.Workflows.KeyNote, shell.Workflows.KeyNoteVoice.IsAnnouncing));
    }, Token);

    [Fact]
    public Task A_note_goes_when_the_page_changes_or_a_sheet_opens() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        Assert.True(shell.Press("B"));
        shell.GoTo(PageId.Home);
        Assert.Equal(string.Empty, shell.Workflows.KeyNote);

        shell.GoTo(PageId.Workflows);
        shell.Workflows.SetViewMode(WorkflowViewMode.List);   // the grouped list: the keys walk its groups
        Assert.True(shell.Press("B"));
        shell.OpenInstance(Detail(Plain).Address, Detail(Plain).Id);

        Assert.Equal((string.Empty, false), (shell.Workflows.KeyNote, shell.Workflows.KeyNoteVoice.IsAnnouncing));
    }, Token);

    [Fact]
    public Task J_and_K_from_a_row_in_a_collapsed_group_step_to_the_nearest_drawn_row() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        var groups = shell.Workflows.Groups;
        var inside = groups[1].Rows[0].Address;
        Assert.True(shell.Workflows.Select(inside));
        groups[1].Toggle();

        Assert.True(shell.Press("J"));
        Assert.Equal(groups[2].Rows[0].Address, shell.Workflows.SelectedAddress, StringComparer.Ordinal);

        Assert.True(shell.Workflows.Select(inside));
        Assert.True(shell.Press("K"));
        Assert.Equal(groups[0].Rows[^1].Address, shell.Workflows.SelectedAddress, StringComparer.Ordinal);
    }, Token);
}
