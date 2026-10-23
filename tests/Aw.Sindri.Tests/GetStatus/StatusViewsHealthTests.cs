using Aw.Context.Testing.Engine;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.GetStatus;

/// <summary>GET /status serves 'views' (the projector's status and its sentence, API spec §2.3) and 'health' (UI spec E-14).</summary>
public sealed class StatusViewsHealthTests
{
    private readonly ViewsHarness _v = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SliceHarness H => _v.Slice;

    private GetStatusService Service() => new(H.Held, new ProfileReads(H.Profiles, H.Profiles), H.Store, H.Events, _v.Progress, new HealthServiceRows(_v.Views, []), new InMemorySessionSnapshot());

    private async Task RunningAndApproved()
    {
        var approved = SliceHarness.Profile("5s");
        H.Profiles.Live = approved;
        _ = await H.HoldLease();
        _ = await new ApproveProfileService(H.Profiles, H.Profiles, H.Held, H.Store, H.Events, H.Challenges, new ProfileChecks(FakeContextEngine.Instance, new FixedAgentCatalog())).Handle(new ApproveProfileRequest(approved.Hash, H.Confirm(approved.Hash)), Token);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_reports_views_and_health()
    {
        await RunningAndApproved();
        _v.Publish(stalledAt: null);

        var status = (await Service().Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal(_v.Progress.Current with { LedgerHead = (await H.Store.Head(Token)).Value }, status.Views);   // the head is the ledger's, read fresh
        Assert.Null(status.Views!.Text);
        Assert.Equal(HealthWord.Healthy, status.Health!.Word);
        Assert.Empty(status.Health.Problems);
        Assert.Null(status.Health.Sentence);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stalled_projector_is_stalled_health_with_the_sentence_beside_it()
    {
        await RunningAndApproved();
        _v.Publish(stalledAt: 2);

        var status = (await Service().Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal(EmptyText.Stalled(2), status.Views!.Text);
        Assert.Equal(HealthWord.Stalled, status.Health!.Word);
        Assert.Equal("Sindri is stalled: the views stopped at event 2.", status.Health.Sentence);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rebuild_is_rebuilding_health()
    {
        await RunningAndApproved();
        _v.Progress.RequestRebuild();

        var status = (await Service().Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal(EmptyText.Rebuilding(0, (await H.Store.Head(Token)).Value), status.Views!.Text);
        Assert.Equal(HealthWord.Rebuilding, status.Health!.Word);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_service_and_an_unapproved_profile_degrade_it()
    {
        _ = (await H.Profiles.Save(SliceHarness.Profile(), Token)).Value;   // heartbeat is in the approved profile; the ledger holds no approval record
        await _v.Fold(EventTypes.ServiceRan, new ServiceRan(H.Ids.New<CommandId>(), ServiceName.From("heartbeat"), H.Clock.GetUtcNow(), ServiceOutcome.Failed, "SND-SCHED-001"));

        var status = (await Service().Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal(HealthWord.Degraded, status.Health!.Word);
        Assert.Equal(["SND-API-001", "SND-SCHED-001", "SND-PROFILE-005"], status.Health.Problems.Select(p => p.Code), StringComparer.Ordinal);
        Assert.Equal("Sindri is degraded: Sindri isn't running, service heartbeat failed, and the profile isn't approved.", status.Health.Sentence);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_built_in_service_degrades_health_like_a_profile_service()
    {
        await RunningAndApproved();
        var gaps = ServiceName.From("sync-gaps");   // the approved profile does not name it
        await _v.Fold(EventTypes.ServiceRan, new ServiceRan(H.Ids.New<CommandId>(), gaps, H.Clock.GetUtcNow(), ServiceOutcome.Failed, "SND-SCHED-001"));
        var service = new GetStatusService(H.Held, new ProfileReads(H.Profiles, H.Profiles), H.Store, H.Events, _v.Progress,
            new HealthServiceRows(_v.Views, [new BuiltInService(gaps, new Cadence(TimeSpan.FromMinutes(1)))]), new InMemorySessionSnapshot());

        var status = (await service.Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal(HealthWord.Degraded, status.Health!.Word);
        Assert.Equal(["SND-SCHED-001"], status.Health.Problems.Select(p => p.Code), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_code_indexs_problems_for_the_approved_profile_are_health_problems()
    {
        await RunningAndApproved();
        var codeIndex = new FixedCodeIndexHealth(new HealthProblem("SND-CODEIDX-014", "demo: code index is behind main; rebuild it from the System page", "the code index for demo is behind main"));
        var service = new GetStatusService(H.Held, new ProfileReads(H.Profiles, H.Profiles), H.Store, H.Events, _v.Progress, new HealthServiceRows(_v.Views, [], codeIndex), new InMemorySessionSnapshot());

        var status = (await service.Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal(["SND-CODEIDX-014"], status.Health!.Problems.Select(p => p.Code), StringComparer.Ordinal);
        Assert.Equal("Sindri is degraded: the code index for demo is behind main.", status.Health.Sentence);
        Assert.Equal(H.Profiles.Live.Value.Hash, codeIndex.AskedWith?.Hash);
    }

    private sealed class FixedCodeIndexHealth(params HealthProblem[] problems) : ICodeIndexHealth
    {
        public LoadedProfile? AskedWith { get; private set; }

        public ValueTask<IReadOnlyList<HealthProblem>> Problems(LoadedProfile? approved, CancellationToken cancellationToken)
        {
            AskedWith = approved;
            return ValueTask.FromResult<IReadOnlyList<HealthProblem>>(problems);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_service_the_approval_dropped_does_not_degrade_health()
    {
        await RunningAndApproved();
        await _v.Fold(EventTypes.ServiceRan, new ServiceRan(H.Ids.New<CommandId>(), ServiceName.From("ghost"), H.Clock.GetUtcNow(), ServiceOutcome.Failed, "SND-SCHED-001"));

        var status = (await Service().Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal(HealthWord.Healthy, status.Health!.Word);
        Assert.Empty(status.Health.Problems);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Views_that_cannot_be_read_are_a_views_problem_not_zero_services()
    {
        await RunningAndApproved();
        _v.Publish(stalledAt: null);
        var unreadable = new FlakyViewStore(_v.Views) { FailReads = true };

        var status = (await new GetStatusService(H.Held, new ProfileReads(H.Profiles, H.Profiles), H.Store, H.Events, _v.Progress, new HealthServiceRows(unreadable, []), new InMemorySessionSnapshot()).Handle(new GetStatusRequest(), Token)).Value;

        Assert.NotEqual(HealthWord.Healthy, status.Health!.Word);
        Assert.Contains(status.Health.Problems, p => p.Code == "SND-VIEW-001");
    }

    [Fact, Trait("Lane", "unit")]
    public void The_displayed_head_is_never_behind_the_cursor_mid_rebuild()
    {
        _v.Progress.Publish(new ViewsStatus(Cursor: 66, LedgerHead: 65, ProjectedAt: null, Rebuilding: true, Stalled: false, StalledAt: null, StalledType: null));
        var views = _v.Progress.Current;

        Assert.Equal(66, views.LedgerHead);
        Assert.Equal(EmptyText.Rebuilding(66, 66), views.Text);
        Assert.Equal(66, views.AsOf.LedgerHead);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_views_json_carries_the_status_fields_and_text_without_a_nested_as_of()
    {
        var views = new ViewsStatus(Cursor: 6, LedgerHead: 9, ProjectedAt: null, Rebuilding: false, Stalled: true, StalledAt: 7, StalledType: "service.ran");

        var json = System.Text.Encoding.UTF8.GetString(AwJson.Serialize(views, SliceHarness.Json));

        Assert.Contains("\"cursor\":6", json, StringComparison.Ordinal);
        Assert.Contains("\"stalledType\":\"service.ran\"", json, StringComparison.Ordinal);
        Assert.Contains("\"text\":\"Views are stalled at 7: run `sindri views rebuild`\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("asOf", json, StringComparison.Ordinal);
    }
}
