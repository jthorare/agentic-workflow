using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>F3 workflows spec §2, §4, §5: the run drawer. Its stages come from the instance golden, its callout from the run's status, and its actions only ask the shell to open the run.</summary>
[Lane(TestLane.Unit)]
public sealed class RunInspectorViewModelTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private readonly FakeSindriApi _server = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static InstanceDetail Build => FixtureInstances.Detail("project-build");

    private static InstanceSummary Served(InstanceDetail detail) => FixtureInstances.List("open").Instances.First(r => string.Equals(r.Id, detail.Id, StringComparison.Ordinal));

    private InstanceRowViewModel Row(InstanceSummary summary) => new(summary, _clipboard, _clock);

    private RunInspectorViewModel Inspector() => new(_server, _clipboard, _clock);

    private async Task<RunInspectorViewModel> Shown(string word)
    {
        var summary = Served(Build) with { Chip = new StateChip(word, ChipTone.Neutral) };
        var inspector = Inspector();
        await inspector.Show(_api, Row(summary), Token);
        return inspector;
    }

    [Fact]
    public async Task Stage_badges_come_from_the_project_build_golden_glyphs_and_child_chips()
    {
        var inspector = Inspector();
        await inspector.Show(_api, Row(Served(Build)), Token);

        Assert.Equal(["Intake", "Scoping", "Design", "Build", "Verify"], inspector.Stages.Select(s => s.Name), StringComparer.Ordinal);
        Assert.Equal(["Done", "Done", "Done", "Waiting for you", "Not started"], inspector.Stages.Select(s => s.Status.Word), StringComparer.Ordinal);   // the build stage has a child waiting on the user
        Assert.Equal([1, 2, 3, 4, 5], inspector.Stages.Select(s => s.Index));
        Assert.Equal("3 of 5 done", inspector.StagesDoneText);
    }

    [Fact]
    public async Task Only_the_current_stage_has_a_time_and_a_step()
    {
        var inspector = Inspector();
        var row = Row(Served(Build));
        await inspector.Show(_api, row, Token);

        var current = Assert.Single(inspector.Stages, s => s.IsCurrent);
        Assert.Equal(("Build", row.Now, Labels.Age(_clock.GetUtcNow() - Build.Since)), (current.Name, current.Detail, current.Time));
        Assert.All(inspector.Stages.Where(s => !s.IsCurrent), s => Assert.Equal((string.Empty, string.Empty), (s.Time, s.Detail)));   // PENDING: finished stages serve no time
    }

    [Fact]
    public async Task The_item_produce_golden_lists_its_own_stages()
    {
        var detail = FixtureInstances.Detail("item-produce");
        var inspector = Inspector();
        var row = Served(Build) with { Id = detail.Id, Address = detail.Address };   // item-produce is a child, not a root row
        await inspector.Show(_api, Row(row), Token);

        Assert.Equal(["Produce", "Checks", "Verify"], inspector.Stages.Select(s => s.Name), StringComparer.Ordinal);
        Assert.Equal("0 of 3 done", inspector.StagesDoneText);
    }

    [Theory]
    [InlineData("DECISION", InspectorCalloutKind.NeedsYou)]
    [InlineData("ESCALATED", InspectorCalloutKind.NeedsYou)]
    [InlineData("PARKED", InspectorCalloutKind.Paused)]
    [InlineData("WITH YOU", InspectorCalloutKind.TookOver)]
    [InlineData("FAILED", InspectorCalloutKind.Failed)]
    [InlineData("DIDN'T WORK OUT", InspectorCalloutKind.Failed)]
    [InlineData("RUNNING", InspectorCalloutKind.Running)]
    [InlineData("DONE", InspectorCalloutKind.None)]
    [InlineData("QUEUED", InspectorCalloutKind.None)]
    [InlineData("CANCELLED", InspectorCalloutKind.None)]
    internal async Task A_callout_per_kind_of_status(string word, InspectorCalloutKind kind)
    {
        var inspector = await Shown(word);

        Assert.Equal(kind, inspector.Callout?.Kind ?? InspectorCalloutKind.None);
        Assert.Equal(kind != InspectorCalloutKind.None, inspector.HasCallout);
    }

    [Fact]
    public async Task The_needs_you_callout_names_what_waits_and_says_the_served_text_else_the_fallback()
    {
        var inspector = await Shown("DECISION");

        Assert.Equal(Labels.RunNeedsTitle(inspector.Row!.Now), inspector.Callout!.Title);
        Assert.Equal(Build.Current.Status.Next, inspector.Callout.Body);
        Assert.True(inspector.HasReview);

        var bare = Inspector();
        await bare.Show(url: null, Row(Served(Build) with { Chip = new StateChip("DECISION", ChipTone.Attn) }), Token);
        Assert.Equal(Labels.RunNeedsBody, bare.Callout!.Body);
    }

    [Fact]
    public async Task The_failed_callout_says_the_fallback_sentence()
    {
        var inspector = await Shown("FAILED");

        Assert.Equal((Labels.WorkflowsStatusFailed, Labels.RunFailedBody), (inspector.Callout!.Title, inspector.Callout.Body));
    }

    [Theory]
    [InlineData("RUNNING", true, false)]
    [InlineData("DECISION", true, false)]
    [InlineData("PARKED", true, false)]
    [InlineData("WITH YOU", false, true)]
    [InlineData("DONE", false, false)]
    [InlineData("FAILED", false, false)]
    [InlineData("CANCELLED", false, false)]
    public async Task Take_over_shows_while_open_and_hand_back_only_when_taken_over(string word, bool takeOver, bool handBack)
    {
        var inspector = await Shown(word);

        Assert.Equal((takeOver, handBack), (inspector.CanTakeOver, inspector.CanHandBack));
    }

    [Theory]
    [InlineData("RUNNING", true)]
    [InlineData("WITH YOU", true)]
    [InlineData("DONE", false)]
    [InlineData("FAILED", false)]
    [InlineData("CANCELLED", false)]
    public async Task Cancel_run_hides_once_the_run_ended(string word, bool shows)
    {
        var inspector = await Shown(word);

        Assert.Equal(shows, inspector.CanCancel);
    }

    private static async Task Until(Func<bool> done)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(TimeSpan.FromSeconds(10));
        while (!done())
        {
            await Task.Delay(10, limit.Token);
        }
    }

    [Fact]
    public async Task Take_over_sends_one_takeover_for_the_instance_and_shows_the_viewer_outcome()
    {
        _server.CommandAnswer = new RouteAnswer(HttpStatusCode.OK, """{"alreadyApplied":false,"viewer":{"kind":"warp","label":"Warp","outcome":"opened"}}""");
        var inspector = await Shown("RUNNING");
        var asked = new List<OpenRequestedEventArgs>();
        inspector.OpenRequested += (_, e) => asked.Add(e);

        inspector.TakeOver();
        await Until(() => inspector.TakeOverLine.Length > 0 && inspector.TakeOverLine != Labels.AttachSending);

        var sent = Assert.Single(_server.CommandCalls);
        Assert.Equal((inspector.Row!.InstanceId, CommandKind.Takeover), (sent.Instance, sent.Body.Kind));
        Assert.Equal(Labels.LaunchOpenedIn("Warp"), inspector.TakeOverLine);
        Assert.Empty(asked);
    }

    [Fact]
    public async Task A_second_press_while_the_takeover_is_out_sends_nothing_and_the_line_says_sending()
    {
        _server.WorkflowCommandGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inspector = await Shown("RUNNING");

        inspector.TakeOver();
        inspector.TakeOver();
        await Until(() => _server.CommandCalls.Count > 0);
        Assert.Equal(Labels.AttachSending, inspector.TakeOverLine);
        _server.WorkflowCommandGate.SetResult();
        await Until(() => inspector.TakeOverLine != Labels.AttachSending);

        _ = Assert.Single(_server.CommandCalls);
    }

    [Fact]
    public async Task A_refused_takeover_shows_its_code_in_the_drawer()
    {
        _server.CommandAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-WF-041", "That run has moved on. Read the instance again.");
        var inspector = await Shown("RUNNING");

        inspector.TakeOver();
        await Until(() => inspector.TakeOverLine.Length > 0 && inspector.TakeOverLine != Labels.AttachSending);

        Assert.Contains("SND-WF-041", inspector.TakeOverLine, StringComparison.Ordinal);
        _ = Assert.Single(_server.CommandCalls);
    }

    [Fact]
    public async Task Open_run_hand_back_and_cancel_run_open_the_run_with_their_intent()
    {
        var inspector = await Shown("WITH YOU");
        var asked = new List<OpenRequestedEventArgs>();
        inspector.OpenRequested += (_, e) => asked.Add(e);

        inspector.OpenRun();
        inspector.HandBack();
        inspector.Cancel();

        Assert.Equal([FrameIntent.None, FrameIntent.HandBack, FrameIntent.AskCancel], asked.Select(a => a.Intent));
        Assert.All(asked, a => Assert.Equal(inspector.Row!.Link, a.Link));
        Assert.Empty(_server.CompleteCalls);
    }

    [Fact]
    public async Task Review_and_decide_goes_to_the_items_needs_you_row_else_the_item_and_approves_nothing()
    {
        var inspector = await Shown("DECISION");
        var asked = new List<OpenRequestedEventArgs>();
        inspector.OpenRequested += (_, e) => asked.Add(e);

        await inspector.ReviewAndDecide(Token);

        var link = Assert.Single(asked).Link;
        Assert.Contains(link.Kind, new[] { DeepLinkKind.NeedsYou, DeepLinkKind.Item, DeepLinkKind.Workflow });
        Assert.Empty(_server.CompleteCalls);
    }

    [Fact]
    public async Task Review_and_decide_opens_the_parked_child_when_no_needs_you_row_names_the_run()
    {
        var instance = System.Text.Json.Nodes.JsonNode.Parse(GoldenFiles.Api("workflow-instance", "project-build"))!;
        instance["itemId"] = null;   // a line run's instance serves no item of its own
        var parked = instance["children"]![0]!["workstreams"]![1]!["rows"]![0]!;
        parked["chip"] = System.Text.Json.Nodes.JsonNode.Parse("""{"word":"PARKED","tone":"neutral"}""");
        _server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, instance.ToJsonString());
        var inspector = Inspector();
        await inspector.Show(_api, Row(Served(Build) with { Chip = new StateChip("PARKED", ChipTone.Neutral) }), Token);
        var asked = new List<OpenRequestedEventArgs>();
        inspector.OpenRequested += (_, e) => asked.Add(e);

        await inspector.ReviewAndDecide(Token);

        var one = Assert.Single(asked);
        Assert.Equal((new DeepLink(DeepLinkKind.Workflow, parked["address"]!.GetValue<string>()), FrameIntent.OpenInitial), (one.Link, one.Intent));
    }

    [Theory]
    [InlineData("scoping", "Scoping")]
    [InlineData("design", "Design")]
    [InlineData("redesign", "Redesign")]
    [InlineData("phase", "Build and prove")]
    public void A_line_stage_reads_its_own_name_not_its_lanes(string state, string name) =>
        Assert.Equal(name, RunInspectorViewModel.StageName(state));

    [Fact]
    public async Task Copy_reference_puts_the_address_on_the_clipboard_and_the_footer_names_it()
    {
        var inspector = await Shown("RUNNING");

        await inspector.CopyReference(Token);

        Assert.Equal(Build.Address, _clipboard.Text);
        Assert.Equal(Labels.RunReferenceNote(Build.Address), inspector.ReferenceNote);
    }

    [Fact]
    public async Task Terminal_copy_shows_only_for_a_protected_answer_and_copies_its_command()
    {
        var protectedRow = FixtureInstances.List("open").Instances.First(r => r.Answer is not null && string.Equals(r.Answer.Class, "protected", StringComparison.Ordinal));
        var inspector = Inspector();
        await inspector.Show(_api, Row(protectedRow), Token);

        Assert.True(inspector.HasTerminal);
        await inspector.CopyTerminal(Token);
        Assert.Equal(protectedRow.Answer!.Cli, _clipboard.Text);

        Assert.False((await Shown("DECISION")).HasTerminal);
    }

    [Fact]
    public async Task Previous_and_next_ask_the_page_to_step_and_follow_the_position()
    {
        var inspector = await Shown("RUNNING");
        var steps = new List<int>();
        inspector.StepRequested += (_, e) => steps.Add(e.Step);

        inspector.SetPosition(3, 12);
        inspector.Previous();
        inspector.Next();

        Assert.Equal(("3 of 12", true, true), (inspector.PositionText, inspector.CanPrevious, inspector.CanNext));
        Assert.Equal([-1, 1], steps);
        inspector.SetPosition(1, 1);
        Assert.Equal((true, false, false), (inspector.PositionText.Length > 0, inspector.CanPrevious, inspector.CanNext));
    }

    [Theory]
    [InlineData(600, 360)]
    [InlineData(900, 432)]
    [InlineData(1000, 480)]
    [InlineData(1440, 480)]
    public void The_drawer_is_48_percent_of_the_window_clamped_to_360_and_480(double window, double width) =>
        Assert.Equal(width, InspectorWidth.For(window), precision: 6);
}
