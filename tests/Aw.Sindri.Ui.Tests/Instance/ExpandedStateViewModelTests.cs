using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>The expanded state (screens §4.2, U8 T3): the engine's status layout as controls; every line a served field, nothing sent.</summary>
[Lane(TestLane.Unit)]
public sealed class ExpandedStateViewModelTests
{
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ExpandedStateViewModel Expanded(string scenario)
    {
        var detail = FixtureInstances.Detail(scenario);
        return new ExpandedStateViewModel(detail.Current, detail.Address, _clipboard, _clock);
    }

    // A status golden the instance goldens don't show in full (taken-over, the parks): the state it sits in is the header's.
    private ExpandedStateViewModel OfStatus(string scenario, StatusFields? edited = null)
    {
        var status = edited ?? FixtureInstances.Status(scenario);
        return new ExpandedStateViewModel(new CurrentState(status.Header.State, Step: null, Gates: [], Desk: null, status), status.Header.Address, _clipboard, _clock);
    }

    private static string TextLine(StatusFields status, string name) =>
        status.Text.Split('\n').Select(l => l.TrimStart()).First(l => l.StartsWith(name, StringComparison.Ordinal))[name.Length..].Trim();

    [Fact]
    public void Shows_step_kind_ref_and_declaration()
    {
        Assert.All(FixtureInstances.Details.Where(d => d.Detail.Current.Step is not null), one =>
        {
            var step = one.Detail.Current.Step!;
            var expanded = Expanded(one.Scenario);
            Assert.True(expanded.HasStep);
            Assert.All((string[])[step.Kind, step.Ref, step.Declaration], part => Assert.Contains(part, expanded.StepLine, StringComparison.Ordinal));
        });
        Assert.False(Expanded("done").HasStep);
    }

    [Fact]
    public void Gates_list_id_on_and_threshold()
    {
        var gates = FixtureInstances.Detail("project-build").Current.Gates;

        var expanded = Expanded("project-build");

        Assert.Equal((true, gates.Count), (expanded.HasGates, expanded.Gates.Count));
        foreach (var (gate, line) in gates.Zip(expanded.Gates))
        {
            Assert.All((string[])[gate.Id, gate.On, gate.Description], part => Assert.Contains(part, line, StringComparison.Ordinal));
            Assert.Equal(gate.Threshold is not null, line.Contains('≥', StringComparison.Ordinal));
        }

        var threshold = gates.First(g => g.Threshold is not null).Threshold!.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains(expanded.Gates, l => l.Contains("≥ " + threshold, StringComparison.Ordinal));
        Assert.False(Expanded("done").HasGates);
    }

    [Fact]
    public void Next_is_the_served_sentence_whole()
    {
        var status = FixtureInstances.Detail("escalated-gate").Current.Status;

        var expanded = Expanded("escalated-gate");

        Assert.Equal((status.Next, true), (expanded.Next, expanded.HasNext));
    }

    [Fact]
    public void Next_line_is_hidden_when_null()
    {
        var expanded = Expanded("done");

        Assert.Null(FixtureInstances.Detail("done").Current.Status.Next);
        Assert.Equal((false, string.Empty), (expanded.HasNext, expanded.Next));
    }

    [Theory]
    [InlineData("project-build")]
    [InlineData("item-produce")]
    public void Budget_reads_state_and_instance(string scenario)
    {
        var status = FixtureInstances.Detail(scenario).Current.Status;

        var expanded = Expanded(scenario);

        Assert.Equal((TextLine(status, "budget"), true), (expanded.BudgetLine, expanded.HasBudget));
        Assert.False(Expanded("incident-loop").HasBudget);
    }

    [Theory]
    [InlineData("dev-desk-round-trip")]
    [InlineData("incident-loop")]
    [InlineData("project-build")]
    public void Timeout_reads_parks_or_escalates_in(string scenario)
    {
        var status = FixtureInstances.Detail(scenario).Current.Status;

        var expanded = Expanded(scenario);

        var line = TextLine(status, "timeout");
        var served = line[..line.IndexOf(" (", StringComparison.Ordinal)];
        Assert.StartsWith(status.Timeout!.Kind == TimeoutKind.Parks ? "parks in" : "escalates in", served, StringComparison.Ordinal);
        Assert.StartsWith(served, expanded.TimeoutLine, StringComparison.Ordinal);
        Assert.Contains($"({status.Timeout!.On})", expanded.TimeoutLine, StringComparison.Ordinal);
        Assert.False(Expanded("done").HasTimeout);
    }

    [Fact]
    public void Taken_over_shows_paused_handback_cancel_and_no_answer()
    {
        var status = FixtureInstances.Status("taken-over");

        var expanded = OfStatus("taken-over");

        Assert.Null(status.Answer);
        Assert.Equal((true, false), (expanded.IsPaused, expanded.HasAnswer));
        Assert.Contains(Labels.Duration(status.Paused!.TimeoutLeftSeconds), expanded.PausedLine, StringComparison.Ordinal);
        Expect.Sequence(status.Commands.Select(c => c.Verb), expanded.Commands.Select(c => c.Verb));
        Assert.Equal([StatusVerb.Handback, StatusVerb.Cancel], expanded.Commands.Select(c => c.Verb));
        Assert.DoesNotContain(expanded.Commands, c => c.Verb == StatusVerb.Takeover);
    }

    [Fact]
    public void Protected_answer_and_ApproveRecipe_render_the_lock_popover()
    {
        var escalated = Expanded("escalated-gate");
        var drift = OfStatus("parked-drift");

        var answer = escalated.Commands.First(c => c.Verb == StatusVerb.Answer);
        var approve = drift.Commands.Single(c => c.Verb == StatusVerb.Approve);

        Assert.True(escalated.HasAnswer);
        Assert.Equal((FixtureInstances.Detail("escalated-gate").Current.Status.Answer!.Cli, Labels.CommandLabel(StatusVerb.Answer, AnswerKind.Accept)), (answer.Protected!.Cli, answer.Label));
        Assert.Equal(FixtureInstances.Status("parked-drift").Commands.Single(c => c.Verb == StatusVerb.Approve).Cli, approve.Protected!.Cli);
        Assert.Equal(Labels.ProtectedLabel(approve.Label), approve.Protected.Label);
        Assert.Null(escalated.Commands.First(c => c.Verb == StatusVerb.Cancel).Protected);
    }

    [Fact]
    public async Task Desk_commands_show_their_cli_pending_U8b()
    {
        var expanded = Expanded("dev-desk-round-trip");
        var served = FixtureInstances.Detail("dev-desk-round-trip").Current.Status.Commands;

        Expect.Sequence(served.Select(c => c.Cli), expanded.Commands.Select(c => c.Cli));
        var pending = expanded.Commands.Where(c => c.Verb != StatusVerb.Takeover).ToList();
        Assert.All(pending, c => Assert.Equal((Labels.AvailableWithU8b, null), (c.PendingChip, c.Protected)));
        Assert.All(pending, c => Assert.Contains(c.PendingChip, c.Name, StringComparison.Ordinal));
        Assert.All(expanded.Commands.Where(c => c.Verb == StatusVerb.Takeover), c => Assert.Equal((string.Empty, true), (c.PendingChip, c.IsTakeOver)));   // Take over is sent by the app: a button, no pending chip
        await expanded.Commands[0].Copy(Token);
        Assert.Equal(served[0].Cli, _clipboard.Text);
    }

    [Fact]
    public void Child_pointer_links_the_child_by_id()
    {
        var status = FixtureInstances.Status("parked-child");
        var expanded = OfStatus("parked-child");
        FrameOpenEventArgs? seen = null;
        expanded.ChildRequested += (_, e) => seen = e;

        expanded.Child!.Open();

        Assert.Equal((status.Child!.Id, status.Child.Address), (seen!.Id, seen.Address));
        Assert.Contains(status.Child.Address, expanded.Child.Text, StringComparison.Ordinal);
        Assert.Contains(status.Child.Address, expanded.Child.Name, StringComparison.Ordinal);
        Assert.Null(OfStatus("taken-over").Child);
    }

    [Fact]
    public void Drift_shows_the_block_and_needs_reapproval()
    {
        var status = FixtureInstances.Status("parked-drift");

        var expanded = OfStatus("parked-drift");

        Assert.True(status.Drift!.NeedsReapproval);
        Assert.True(expanded.HasDrift);
        Assert.Contains(status.Drift.Block, expanded.DriftLine, StringComparison.Ordinal);
        Assert.Contains(Labels.NeedsReapproval, expanded.DriftLine, StringComparison.Ordinal);
        Assert.False(OfStatus("taken-over").HasDrift);
    }

    [Fact]
    public void Done_shows_no_commands()
    {
        var expanded = Expanded("done");

        Assert.Empty(FixtureInstances.Detail("done").Current.Status.Commands);
        Assert.Equal((false, false, false), (expanded.HasCommands, expanded.HasAnswer, expanded.HasAttach));
    }

    [Fact]
    public async Task Status_text_is_copied_verbatim()
    {
        var expanded = Expanded("escalated-gate");

        await expanded.CopyStatus(Token);

        Assert.Equal(FixtureInstances.Detail("escalated-gate").Current.Status.Text, _clipboard.Text);
        Assert.Equal(Labels.Copied, expanded.StatusCopy.Label);
        Assert.Contains(FixtureInstances.Detail("escalated-gate").Address, expanded.CopyStatusName, StringComparison.Ordinal);
    }

    [Fact]
    public void Cancel_shows_the_served_confirm_with_Keep_focused()
    {
        var served = FixtureInstances.Detail("project-build").Current.Status.Commands.Single(c => c.Verb == StatusVerb.Cancel).Confirm;
        var expanded = Expanded("project-build");
        Assert.False(expanded.IsConfirmingCancel);

        expanded.AskCancel();

        Assert.Equal((true, served, ConfirmChoice.Keep), (expanded.IsConfirmingCancel, expanded.ConfirmLine, expanded.ConfirmFocus));
        expanded.Keep();
        Assert.Equal((false, string.Empty), (expanded.IsConfirmingCancel, expanded.PendingNote));
        expanded.AskCancel();
        expanded.ConfirmCancel();
        Assert.Equal((false, Labels.SendingArrivesNote), (expanded.IsConfirmingCancel, expanded.PendingNote));
    }

    [Fact]
    public void Cancel_falls_back_to_Cancel_address_only_when_confirm_is_null()
    {
        var node = GoldenFiles.Node("workflow-status", "taken-over");
        node["commands"]!.AsArray().First(c => (string)c!["verb"]! == "cancel")!["confirm"] = null;
        var status = FixtureInstances.Parse<StatusFields>(node);

        var expanded = OfStatus("taken-over", status);
        expanded.AskCancel();

        Assert.Equal(Labels.CancelAsk(status.Header.Address), expanded.ConfirmLine);
    }

    [Fact]
    public void An_agent_step_shows_Attach_waiting_on_the_item_not_P10a()
    {
        var agent = Expanded("incident-loop");
        var desk = Expanded("dev-desk-round-trip");

        Assert.Equal((true, true, Labels.AttachChecking), (agent.HasAttach, agent.Attach!.IsPending, agent.Attach.PendingChip));
        Assert.Contains(FixtureInstances.Detail("incident-loop").Address, agent.Attach.PendingChipName, StringComparison.Ordinal);
        Assert.Equal((false, null), (desk.HasAttach, desk.Attach));
    }

    [Fact]
    public void Each_answer_variant_shows_its_own_label_and_served_cli()
    {
        var served = FixtureInstances.Status("escalated-gate").Commands.Where(c => c.Verb == StatusVerb.Answer).ToList();

        var answers = OfStatus("escalated-gate").Commands.Where(c => c.Verb == StatusVerb.Answer).ToList();

        Assert.Equal(served.Select(c => (Labels.Action(c.ActionId), c.Cli)), answers.Select(a => (a.Label, a.Cli)));
        Assert.Equal(["Accept", "Reject"], answers.Select(a => a.Label), StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_answer_entry_copies_its_served_cli_not_the_status_line_hint()
    {
        var status = FixtureInstances.Status("needs-answer-wait");
        var answer = OfStatus("needs-answer-wait").Commands.Single(c => c.Verb == StatusVerb.Answer);

        await answer.Copy(Token);

        var cli = status.Commands.Single(c => c.Verb == StatusVerb.Answer).Cli;
        Assert.Equal((cli, cli), (answer.Cli, _clipboard.Text));
        Assert.NotEqual(status.Answer!.Cli, answer.Cli, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("escalated-child")]
    [InlineData("mirrored-budget")]
    [InlineData("mirrored-budget-no-child")]
    public void A_child_pointer_answer_shows_no_answer_command(string scenario)
    {
        var status = FixtureInstances.Status(scenario);

        var expanded = OfStatus(scenario);

        Assert.NotNull(status.Answer);
        Assert.DoesNotContain(status.Commands, c => c.Verb == StatusVerb.Answer);
        Assert.False(expanded.HasAnswer);
        Assert.All(expanded.Commands, c => Assert.NotEmpty(c.Cli));
    }
}
