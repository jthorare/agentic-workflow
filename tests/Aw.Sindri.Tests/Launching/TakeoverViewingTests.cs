using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ApplyCommand;
using Aw.Sindri.Features.Launching;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launching;

/// <summary>A takeover opens the Step's live session in the viewer and records the outcome; nothing the viewer does stops the takeover (issue 1004).</summary>
[Lane(TestLane.Unit)]
public sealed class TakeoverViewingTests
{
    private static readonly InstanceAddress _instance = InstanceAddress.From("ITM-00000000000000000000000000000001/spec/1");
    private static readonly CommandNonce _nonce = CommandNonce.From(new string('c', 32));
    private static readonly string[] _attach = ["tmux", "-L", "sindri-launch", "attach", "-t", "=snd-" + "LCH-00000000000000000000000000000001"];

    private readonly LauncherRig _rig = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private TakeoverViewing Viewing => new(new LaunchViews(_rig.H.Views), _rig.Viewer, _rig.Slice.Held, _rig.Slice.Store, _rig.Slice.Events);

    private async Task Live()
    {
        await _rig.Boot();
        await _rig.Item();
        var outcomes = new LaunchOutcomes(_rig.Slice.Store, _rig.Slice.Events, _rig.Viewer, new LaunchLiveness(), _rig.Slice.Clock);
        var request = A.LaunchRequest().Unattended(new StepArgs(Model: null, Policy: null, new StepRef("challenge", "agent"))).OnInstance(_instance, RunToken.For(1, 1)).Build();
        _ = await outcomes.Started(_rig.Grant, request, new StartedLaunch(new HostRef(HostKind.Tmux, "snd-x"), ViewerOutcome.Skipped, _attach, CatalogSource.Builtin, "sha256:abc"), Token);
        await _rig.Sync();
    }

    [Fact]
    public async Task A_takeover_opens_the_live_session_in_the_viewer_and_records_it()
    {
        _rig.Viewer = new FakeTerminalViewer("warp", "Warp");
        await Live();

        var shown = await Viewing.Open(_instance, _nonce, Token);

        Expect.Equal(("warp", "Warp", ViewerOutcomeKind.Opened, null), (shown!.Viewer.Kind, shown.Viewer.Label, shown.Viewer.Outcome, shown.Viewer.Reason));
        Assert.Equal(_attach, shown.Attach);
        Assert.Equal(_attach, Assert.Single(_rig.Viewer.Requests).AttachArgv);
        await _rig.Sync();   // the launches view folds the event before it is read
        var recorded = _rig.Payload<LaunchTakeoverOpened>(Assert.Single(_rig.Recorded(EventTypes.LaunchTakeoverOpened)));
        Expect.Equal((_instance, ViewerOutcomeKind.Opened), (recorded.Instance, recorded.Viewer.Outcome));
        Expect.Equal(ViewerOutcomeKind.Opened, (await new LaunchViews(_rig.H.Views).Get(A.FirstLaunchId, Token)).Value!.Viewer!.Outcome);   // the launch read shows the latest outcome after folding the event
    }

    [Fact]
    public async Task A_viewer_that_fails_reports_the_reason_and_the_attach_command()
    {
        var viewer = new FakeTerminalViewer("warp", "Warp");
        viewer.Script(ViewerOutcome.Failed("SND-LAUNCH-001", "private stderr"));
        _rig.Viewer = viewer;
        await Live();

        var shown = await Viewing.Open(_instance, _nonce, Token);

        Assert.Equal(ViewerOutcomeKind.Failed, shown!.Viewer.Outcome);
        Assert.Equal("SND-LAUNCH-001", shown.Viewer.Code);
        Assert.NotNull(shown.Viewer.Reason);
        Assert.Null(shown.Viewer.Detail);
        Assert.Equal(_attach, shown.Attach);
        _ = Assert.Single(_rig.Recorded(EventTypes.LaunchTakeoverOpened));
    }

    [Fact]
    public async Task A_box_with_no_usable_viewer_reports_none_usable_with_the_attach_command()
    {
        var viewer = new FakeTerminalViewer("print", "Print");
        viewer.Script(ViewerOutcome.Skipped);
        _rig.Viewer = viewer;
        await Live();

        var shown = await Viewing.Open(_instance, _nonce, Token);

        Assert.Equal(ViewerOutcomeKind.Failed, shown!.Viewer.Outcome);
        Assert.Equal("no-viewer", shown.Viewer.Code);
        Assert.NotNull(shown.Viewer.Reason);
        Assert.Equal(_attach, shown.Attach);
    }

    [Fact]
    public async Task A_failed_viewer_leaves_the_takeover_accepted()
    {
        await Live();
        _rig.Viewer = Scripted(ViewerOutcome.Failed("SND-LAUNCH-001"));
        var command = new TakeoverViewingCommand(new AppliedTakeover(), Viewing);

        var result = await command.Handle(new ApplyCommandRequest(_instance.Value, ApplyCommandKind.Takeover), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal("taken over", result.Value.Result.Happened);
        Assert.Equal(ViewerOutcomeKind.Failed, result.Value.Viewer!.Outcome);
        Assert.Equal(_attach, result.Value.Attach);
    }

    [Fact]
    public async Task A_takeover_that_was_already_applied_opens_no_viewer()
    {
        await Live();
        var command = new TakeoverViewingCommand(new AppliedTakeover(alreadyApplied: true), Viewing);

        var result = await command.Handle(new ApplyCommandRequest(_instance.Value, ApplyCommandKind.Takeover), Token);

        Assert.Null(result.Value.Viewer);
        Assert.Empty(_rig.Viewer.Requests);
    }

    /// <summary>The apply-command service as far as the decorator reads it: every takeover applies.</summary>
    private sealed class AppliedTakeover(bool alreadyApplied = false) : IService<ApplyCommandRequest, ApplyCommandResponse>
    {
        private static readonly RecipeVersion _recipe = new(RecipeId.From("workflow.step"), 1, RecipeHash.From("sha256:" + new string('a', 64)));

        public ValueTask<Result<ApplyCommandResponse>> Handle(ApplyCommandRequest request, CancellationToken cancellationToken)
        {
            var view = new InstanceView(_instance, _recipe, "taken-over", Detail: null, DateTimeOffset.FromUnixTimeMilliseconds(1_000), string.Empty, [], Question: null, Task: null, Run: null, Children: [], Outcome: null)
            { Id = WorkflowInstanceId.Of(_instance), Version = 1 };
            return ValueTask.FromResult<Result<ApplyCommandResponse>>(new ApplyCommandResponse(new CommandResult(view, "taken over"), _nonce, alreadyApplied));
        }
    }

    private static FakeTerminalViewer Scripted(ViewerOutcome outcome)
    {
        var viewer = new FakeTerminalViewer("warp", "Warp");
        viewer.Script(outcome);
        return viewer;
    }

    [Fact]
    public async Task An_instance_with_no_live_session_opens_nothing_and_records_nothing()
    {
        await _rig.Boot();
        await _rig.Item();

        var shown = await Viewing.Open(_instance, _nonce, Token);

        Assert.Null(shown);
        Assert.Empty(_rig.Viewer.Requests);
        Assert.Empty(_rig.Recorded(EventTypes.LaunchTakeoverOpened));
    }

    [Fact]
    public async Task A_background_launch_records_the_configured_viewer_and_why_it_was_skipped()
    {
        await _rig.Boot();
        await _rig.Item();
        var outcomes = new LaunchOutcomes(_rig.Slice.Store, _rig.Slice.Events, _rig.Viewer, new LaunchLiveness(), _rig.Slice.Clock);
        var request = A.LaunchRequest().Unattended(new StepArgs(Model: null, Policy: null, new StepRef("challenge", "agent"))).OnInstance(_instance, RunToken.For(1, 1)).Build();
        var skipped = ViewerOutcome.Skipped with { ViewerKind = "warp", ViewerLabel = "Warp", Reason = "background: viewer opens on takeover" };

        _ = await outcomes.Started(_rig.Grant, request, new StartedLaunch(new HostRef(HostKind.Tmux, "snd-x"), skipped, _attach, CatalogSource.Builtin, "sha256:abc"), Token);

        var viewer = _rig.Payload<LaunchStarted>(Assert.Single(_rig.Recorded(EventTypes.LaunchStarted))).Viewer;
        Expect.Equal(("warp", ViewerOutcomeKind.Skipped, "background: viewer opens on takeover"), (viewer.Kind, viewer.Outcome, viewer.Reason));
    }
}
