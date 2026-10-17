using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.GetLaunch;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.GetLaunch;

/// <summary>GET /launches/{launchId} (UI spec E-1, launcher L-10): what became of a click, from the 'launches' view; a session sees its own repo's launches only.</summary>
[Lane(TestLane.Unit)]
public sealed class GetLaunchTests
{
    private readonly ViewsHarness _v = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GetLaunchService Service() => new(new LaunchViews(_v.Views), _v.Progress, _v.Slice.Callers);

    private IDisposable Human() => _v.Slice.Callers.Enter(A.Caller().Human().Build());

    [Fact]
    public async Task GetLaunch_returns_a_requested_launch()
    {
        await _v.Item(repo: null);
        await _v.Request();
        using var entered = Human();

        var result = await Service().Handle(A.GetLaunchRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var view = result.Value;
        Expect.Equal((A.FirstLaunchId, LaunchState.Requested, "click"), (view.Id, view.State, view.ChosenBy));
        Assert.Null(view.Viewer);
        Expect.Equal(_v.Progress.Current.AsOf, view.AsOf);
    }

    [Fact]
    public async Task A_started_launch_carries_its_viewer_attach_and_resumable_session()
    {
        await _v.Item(repo: null);
        await _v.Request();
        await _v.Start(outcome: ViewerOutcomeKind.Unconfirmed);
        await _v.Reported(SessionId.From("sess-1"));
        using var entered = Human();

        var view = (await Service().Handle(A.GetLaunchRequest().Build(), Token)).Value;

        Expect.Equal((LaunchState.Started, ViewerOutcomeKind.Unconfirmed, "sess-1"), (view.State, view.Viewer!.Outcome, view.SessionId!.Value.Value));
        Assert.NotEmpty(view.Attach!);
    }

    [Fact]
    public async Task A_failed_launch_carries_its_code_detail_and_stage()
    {
        await _v.Item(repo: null);
        await _v.Request();
        await _v.Fail("SND-LAUNCH-009", LaunchStage.Host);
        using var entered = Human();

        var view = (await Service().Handle(A.GetLaunchRequest().Build(), Token)).Value;

        Expect.Equal((LaunchState.Failed, "SND-LAUNCH-009", LaunchStage.Host), (view.State, view.Code, view.Stage));
        Assert.NotNull(view.Detail);
    }

    [Fact]
    public async Task An_ended_session_shows_how()
    {
        await _v.Item(repo: null);
        await _v.Request();
        await _v.Start();
        await _v.End(SessionEndReason.Lost);
        using var entered = Human();

        var view = (await Service().Handle(A.GetLaunchRequest().Build(), Token)).Value;

        Expect.Equal(SessionEndReason.Lost, view.Ended!.Reason);
    }

    [Fact]
    public async Task Unknown_launch_is_SND_LAUNCH_404()
    {
        using var entered = Human();

        var result = await Service().Handle(A.GetLaunchRequest().Build(), Token);

        Expect.Equal(SindriErrors.LaunchNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Session_sees_its_own_repos_launches_only()
    {
        await _v.Item(RepoName.From("alpha"));
        await _v.Request();
        using (_v.Slice.Callers.Enter(A.Caller().Session().InRepo("alpha").Build()))
        {
            Assert.True((await Service().Handle(A.GetLaunchRequest().Build(), Token)).IsSuccess);
        }

        using var other = _v.Slice.Callers.Enter(A.Caller().Session().InRepo("beta").Build());
        var hidden = await Service().Handle(A.GetLaunchRequest().Build(), Token);
        Expect.Equal(SindriErrors.LaunchNotFound.Code, hidden.Error.Code);   // never a hint that it exists
    }
}
