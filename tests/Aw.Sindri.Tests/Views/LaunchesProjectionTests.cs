using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>The 'launches' view fold through the projector's pages (UI spec E-1, launcher L-9, L-10): the launch events make and change one row per launch.</summary>
[Lane(TestLane.Unit)]
public sealed class LaunchesProjectionTests
{
    private static readonly RepoName _repo = RepoName.From("demo");
    private readonly ViewsHarness _v = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<LaunchRow?> Row() => (await _v.Views.Get<LaunchRow>(ViewNames.Launches, A.FirstLaunchId.Value, Token)).Value;

    [Fact]
    public async Task A_failure_keeps_its_sandbox_reason_and_source()
    {
        await _v.Request();
        await _v.Fail("SND-LAUNCH-017", reason: "expired", source: "separate-login");

        var row = (await Row())!;

        Expect.Equal(("SND-LAUNCH-017", "expired", "separate-login"), (row.Code, row.Reason, row.Source));
    }

    [Fact]
    public async Task A_limit_ending_keeps_its_kind_limit_and_peak()
    {
        await _v.Request();
        await _v.Start();
        await _v.EndByLimit(new LimitEnd(LimitKind.Memory, 12_884_901_888, 13_000_000_000));

        var ended = (await Row())!.Ended!;

        Expect.Equal((SessionEndReason.Limit, LimitKind.Memory, 12_884_901_888L, 13_000_000_000L), (ended.Reason, ended.Kind, ended.Limit, ended.Peak));
    }

    [Fact]
    public async Task An_exit_ending_has_no_limit_fields()
    {
        await _v.Request();
        await _v.Start();
        await _v.End();

        var ended = (await Row())!.Ended!;

        Expect.Equal((null, null, null), (ended.Kind, ended.Limit, ended.Peak));
    }

    [Fact]
    public async Task A_request_makes_a_requested_row_with_the_items_repo()
    {
        await _v.Item(_repo);
        await _v.Request(target: AgentId.From("codex"), mode: LaunchMode.Resume, chosenBy: "default");

        var row = await Row();

        Expect.Equal((LaunchState.Requested, _repo, AgentId.From("codex"), LaunchMode.Resume, "default"), (row!.State, row.Repo!.Value, row.Target, row.Mode, row.ChosenBy));
        Assert.Null(row.OutcomeAt);
    }

    [Fact]
    public async Task A_start_keeps_the_viewer_attach_and_session()
    {
        await _v.Item(_repo);
        await _v.Request();
        await _v.Start(outcome: ViewerOutcomeKind.Failed, session: SessionId.From("sess-1"), viewerDetail: "boom\u001b[31m");

        var row = (await Row())!;

        Expect.Equal((LaunchState.Started, ViewerOutcomeKind.Failed, "sess-1"), (row.State, row.Viewer!.Outcome, row.SessionId!.Value.Value));
        Assert.DoesNotContain("\u001b", row.Viewer.Detail!, StringComparison.Ordinal);   // the viewer's stderr is escaped again on its way into the view
        Assert.True(row.OutcomeAt is not null);
        Assert.True(row.Live);
    }

    [Fact]
    public async Task A_late_attach_turns_unconfirmed_into_opened_late()
    {
        await _v.Item(_repo);
        await _v.Request();
        await _v.Start(outcome: ViewerOutcomeKind.Unconfirmed);
        await _v.Attached();

        var row = (await Row())!;

        Expect.Equal((ViewerOutcomeKind.Opened, true), (row.Viewer!.Outcome, row.Late));
    }

    [Fact]
    public async Task A_late_attach_leaves_a_failed_viewer_failed()
    {
        await _v.Item(_repo);
        await _v.Request();
        await _v.Start(outcome: ViewerOutcomeKind.Failed);
        await _v.Attached();

        var row = (await Row())!;

        Expect.Equal((ViewerOutcomeKind.Failed, false), (row.Viewer!.Outcome, row.Late));
    }

    [Fact]
    public async Task A_failure_keeps_its_code_message_and_stage()
    {
        await _v.Item(_repo);
        await _v.Request();
        await _v.Fail("SND-LAUNCH-009", LaunchStage.Host, message: "timed out");

        var row = (await Row())!;

        Expect.Equal((LaunchState.Failed, "SND-LAUNCH-009", "timed out", LaunchStage.Host), (row.State, row.Code, row.Message, row.Stage));
        Assert.False(row.Live);
    }

    [Fact]
    public async Task The_first_outcome_wins()
    {
        await _v.Item(_repo);
        await _v.Request();
        await _v.Start();
        await _v.Fail();

        Expect.Equal(LaunchState.Started, (await Row())!.State);
    }

    [Fact]
    public async Task A_reported_id_and_an_end_are_kept()
    {
        await _v.Item(_repo);
        await _v.Request();
        await _v.Start();
        await _v.Reported(SessionId.From("reported-1"));
        await _v.End(SessionEndReason.Lost);

        var row = (await Row())!;

        Expect.Equal(("reported-1", SessionEndReason.Lost, false), (row.ResumableId!.Value.Value, row.Ended!.Reason, row.Live));
    }

    [Fact]
    public async Task The_id_known_at_start_wins_over_the_reported_one()
    {
        await _v.Item(_repo);
        await _v.Request();
        await _v.Start(session: SessionId.From("at-start"));
        await _v.Reported(SessionId.From("reported-1"));

        Expect.Equal("at-start", (await Row())!.ResumableId!.Value.Value);
    }

    [Fact]
    public async Task An_engine_launch_with_no_request_makes_its_row_at_the_start()
    {
        await _v.Item(_repo);
        await _v.Start();

        var row = (await Row())!;

        Expect.Equal((LaunchState.Started, "engine", _repo), (row.State, row.ChosenBy, row.Repo!.Value));
    }

    [Fact]
    public async Task An_outcome_for_a_launch_that_has_no_row_is_skipped_and_never_stalls_the_projector()
    {
        var ct = Token;
        _ = (await _v.Views.Open(ViewSchema.Version, ct)).Value;
        var now = _v.Slice.Clock.GetUtcNow();
        var orphans = new[]
        {
            Recorded.Of(_v.Slice.Events, EventTypes.LaunchViewerAttached, new LaunchViewerAttached(A.FirstLaunchId, now), 1),
            Recorded.Of(_v.Slice.Events, EventTypes.LaunchFailed, new LaunchFailed(A.FirstLaunchId, A.FirstItemId, "SND-LAUNCH-007", "gone"), 2),
            Recorded.Of(_v.Slice.Events, EventTypes.LaunchSessionReported, new LaunchSessionReported(A.FirstLaunchId, SessionId.From("reported-1")), 3),
            Recorded.Of(_v.Slice.Events, EventTypes.SessionEnded, new SessionEnded(A.FirstLaunchId, SessionEndReason.Lost, ExitCode: null), 4),
        };

        var applied = (await _v.Views.Apply(orphans, [new LaunchesProjection(_v.Slice.Events)], now, ct)).Value;

        Assert.Null(applied.Stall);
        Assert.Null((await _v.Views.Get<LaunchRow>(ViewNames.Launches, A.FirstLaunchId.Value, ct)).Value);
    }

    [Fact]
    public async Task Launches_projection_survives_rebuild_with_session_reported_id()
    {
        var ct = Token;
        var projections = new IProjection[] { new ItemsProjection(_v.Slice.Events, new PassThroughScrubber()), new LaunchesProjection(_v.Slice.Events) };
        var events = new[]
        {
            Recorded.Of(_v.Slice.Events, EventTypes.ItemCreated, new ItemCreated(_v.Slice.Ids.New<CommandId>(), A.FirstItemId, "Ship it", _repo, Link: null, Body: null, Owner: null), 1, A.FirstItemId),
            Recorded.Of(_v.Slice.Events, EventTypes.ItemOpenRequested, new ItemOpenRequested(A.FirstLaunchId, A.FirstItemId, LaunchMode.Open, LaunchFolds.ClaudeCode, "click"), 2, A.FirstItemId),
            Recorded.Of(_v.Slice.Events, EventTypes.LaunchStarted, new LaunchStarted(A.FirstLaunchId, A.FirstItemId, LaunchMode.Open, LaunchFolds.ClaudeCode, SessionId: null, "sindri", new LaunchViewer("warp", "Warp", ViewerOutcomeKind.Unconfirmed, Code: null, Detail: null), ["tmux"], CatalogSource.Builtin, "sha256:abc"), 3, A.FirstItemId),
            Recorded.Of(_v.Slice.Events, EventTypes.LaunchViewerAttached, new LaunchViewerAttached(A.FirstLaunchId, _v.Slice.Clock.GetUtcNow()), 4, A.FirstItemId),
            Recorded.Of(_v.Slice.Events, EventTypes.LaunchSessionReported, new LaunchSessionReported(A.FirstLaunchId, SessionId.From("reported-1")), 5, A.FirstItemId),
        };
        _ = (await _v.Views.Open(ViewSchema.Version, ct)).Value;
        _ = (await _v.Views.Apply(events, projections, _v.Slice.Clock.GetUtcNow(), ct)).Value;
        var incremental = await Row();

        _ = (await _v.Views.Reset([ViewNames.Items, ViewNames.Launches], ViewSchema.Version, _v.Slice.Clock.GetUtcNow(), ct)).Value;
        _ = (await _v.Views.Apply(events, projections, _v.Slice.Clock.GetUtcNow(), ct)).Value;
        var rebuilt = await Row();

        Expect.Equal("reported-1", rebuilt!.ReportedSessionId!.Value.Value);
        Expect.Equal(System.Text.Encoding.UTF8.GetString(AwJson.Serialize(incremental!, SliceHarness.Json)), System.Text.Encoding.UTF8.GetString(AwJson.Serialize(rebuilt, SliceHarness.Json)));   // the same bytes
    }
}
