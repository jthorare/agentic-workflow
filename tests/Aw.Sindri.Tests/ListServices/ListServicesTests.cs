using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ListServices;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.ListServices;

/// <summary>GET /services (API spec §3.1): the approved profile's services joined with the 'services' view (plan G5).</summary>
public sealed class ListServicesTests
{
    private readonly ViewsHarness _v = new();
    private readonly InMemoryJobViews _jobs = new();
    private readonly List<BuiltInService> _builtIns = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task ListServices_lists_each_approved_service_with_its_last_run()
    {
        await Approve(("heartbeat", "30s"), ("digest", "5m"));
        var slot = _v.Slice.Clock.GetUtcNow();
        await Ran("heartbeat", slot, ServiceOutcome.Failed, "SND-SCHED-001");

        var result = await Service().Handle(A.ListServicesRequest().Build(), Token);

        Assert.True(result.IsSuccess);
        var list = result.Value;
        Assert.Equal(["digest", "heartbeat"], list.Services.Select(s => s.Name.Value), StringComparer.Ordinal);
        var (digest, heartbeat) = (list.Services[0], list.Services[1]);
        Assert.Equal(("5m", false), (digest.Every, digest.Paused));
        Assert.Null(digest.LastRun);
        Assert.Null(digest.NextSlot);   // before the first run (G5)
        Assert.Equal(("30s", slot, ServiceOutcome.Failed, "SND-SCHED-001"), (heartbeat.Every, heartbeat.LastRun!.Slot, heartbeat.LastRun.Outcome, heartbeat.LastRun.Code));
        Assert.Null(list.Empty);
        Assert.Equal(_v.Progress.Current.AsOf, list.AsOf);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Next_slot_is_last_slot_plus_cadence()
    {
        await Approve(("heartbeat", "30s"));
        var slot = _v.Slice.Clock.GetUtcNow();
        await Ran("heartbeat", slot, ServiceOutcome.Ok, code: null);

        var heartbeat = Assert.Single((await Service().Handle(new ListServicesRequest(), Token)).Value.Services);

        Assert.Equal(slot.AddSeconds(30), heartbeat.NextSlot);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_service_the_approval_dropped_leaves_the_list()
    {
        await Approve(("heartbeat", "30s"));
        await Ran("retired", _v.Slice.Clock.GetUtcNow(), ServiceOutcome.Ok, code: null);

        var list = (await Service().Handle(new ListServicesRequest(), Token)).Value;

        Assert.Equal(["heartbeat"], list.Services.Select(s => s.Name.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_built_in_is_listed_with_its_cadence_and_a_profile_entry_of_its_name_sets_the_cadence()
    {
        _builtIns.AddRange([new BuiltInService(ServiceName.From("sync-gaps"), new Cadence(TimeSpan.FromMinutes(1))), new BuiltInService(ServiceName.From("heartbeat"), new Cadence(TimeSpan.FromMinutes(9)))]);
        await Approve(("heartbeat", "30s"));
        await Ran("sync-gaps", _v.Slice.Clock.GetUtcNow(), ServiceOutcome.Failed, "SND-X-001");

        var list = (await Service().Handle(new ListServicesRequest(), Token)).Value;

        Assert.Equal([("heartbeat", "30s"), ("sync-gaps", "1m")], list.Services.Select(s => (s.Name.Value, s.Every)));
        Assert.Equal("SND-X-001", list.Services[1].LastRun!.Code);
        Assert.Null(list.Empty);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Empty_text_when_no_services()
    {
        var list = (await Service().Handle(new ListServicesRequest(), Token)).Value;

        Assert.Empty(list.Services);
        Assert.Equal(EmptyText.Services(), list.Empty);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Rebuilding_and_stalled_answers_carry_their_sentence()
    {
        await Approve(("heartbeat", "30s"));
        await Ran("heartbeat", _v.Slice.Clock.GetUtcNow(), ServiceOutcome.Ok, code: null);
        _v.Progress.RequestRebuild();
        var rebuilding = (await Service().Handle(new ListServicesRequest(), Token)).Value;
        _ = _v.Progress.TakeRebuildRequest();
        _v.Publish(stalledAt: 2);

        var stalled = (await Service().Handle(new ListServicesRequest(), Token)).Value;

        Assert.Equal((EmptyText.Rebuilding(1, 1), true), (rebuilding.Empty, rebuilding.AsOf.Rebuilding));
        Assert.Equal((EmptyText.Stalled(2), true), (stalled.Empty, stalled.AsOf.Stalled));
        _ = Assert.Single(stalled.Services);   // the rows still answer, with the sentence beside them
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_projector_that_cannot_run_answers_with_its_own_sentence_not_an_event_number()
    {
        await Approve(("heartbeat", "30s"));
        _v.Progress.Publish(_v.Progress.Current with { Stalled = true, StalledAt = null, StalledType = "SND-VIEW-001: views.db can't be opened" });

        var list = (await Service().Handle(new ListServicesRequest(), Token)).Value;

        Assert.Equal(_v.Progress.Current.Text, list.Empty);
        Assert.Contains("can't be updated (SND-VIEW-001: views.db can't be opened)", list.Empty, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_paused_service_is_served_paused_with_no_next_slot()
    {
        await Approve(("heartbeat", "30s"));
        var slot = _v.Slice.Clock.GetUtcNow();
        await Ran("heartbeat", slot, ServiceOutcome.Ok, code: null);
        await _v.Fold(EventTypes.ServicePaused, new ServicePaused(_v.Slice.Ids.New<CommandId>(), ServiceName.From("heartbeat")));

        var heartbeat = Assert.Single((await Service().Handle(new ListServicesRequest(), Token)).Value.Services);

        Assert.True(heartbeat.Paused);
        Assert.Null(heartbeat.NextSlot);
        Assert.Equal(slot, heartbeat.LastRun!.Slot);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Next_slot_ignores_a_manual_run()
    {
        await Approve(("heartbeat", "30s"));
        var slot = _v.Slice.Clock.GetUtcNow();
        await Ran("heartbeat", slot, ServiceOutcome.Ok, code: null);
        await _v.Fold(EventTypes.ServiceRan, new ServiceRan(_v.Slice.Ids.New<CommandId>(), ServiceName.From("heartbeat"), slot.AddSeconds(7), ServiceOutcome.Ok, Code: null, A.FirstJobId));

        var heartbeat = Assert.Single((await Service().Handle(new ListServicesRequest(), Token)).Value.Services);

        Assert.Equal(slot.AddSeconds(30), heartbeat.NextSlot);   // the scheduler's cadence, not the manual start
        Assert.Equal(slot.AddSeconds(7), heartbeat.LastRun!.Slot);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_running_job_of_a_service_is_served_on_the_service()
    {
        await Approve(("heartbeat", "30s"), ("digest", "5m"));
        _jobs.Put(A.Job().Build());   // a running RunService job for heartbeat

        var list = (await Service().Handle(new ListServicesRequest(), Token)).Value;

        Assert.Equal(A.FirstJobId, list.Services.Single(s => s.Name.Value == "heartbeat").Job);
        Assert.Null(list.Services.Single(s => s.Name.Value == "digest").Job);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Actions_name_what_the_server_would_allow_for_each_service()
    {
        await Approve(("heartbeat", "30s"), ("digest", "5m"));
        _builtIns.Add(new BuiltInService(ServiceName.From("workflow-events"), new Cadence(TimeSpan.FromSeconds(5))));
        await _v.Fold(EventTypes.ServicePaused, new ServicePaused(_v.Slice.Ids.New<CommandId>(), ServiceName.From("digest")));
        _jobs.Put(A.Job().Build());   // a running RunService job for heartbeat

        var list = (await Service().Handle(new ListServicesRequest(), Token)).Value;

        Assert.Equal(
            ["digest: run,resume", "heartbeat: pause", "workflow-events: run"],
            list.Services.Select(s => $"{s.Name.Value}: {string.Join(',', s.Actions)}"),
            StringComparer.Ordinal);
    }

    private ListServicesService Service() => new(_v.Slice.Profiles, _v.Views, _v.Progress, _jobs, _builtIns);

    private async Task Approve(params (string Name, string Every)[] services) =>
        _ = (await _v.Slice.Profiles.Save(SliceHarness.ProfileOf("thor", services), Token)).Value;

    private Task Ran(string service, DateTimeOffset slot, ServiceOutcome outcome, string? code) => _v.Fold(EventTypes.ServiceRan,
        new ServiceRan(_v.Slice.Ids.New<CommandId>(), ServiceName.From(service), slot, outcome, code));
}
