using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.SetupWatch;
using Aw.Sindri.Tests.AddRepo;

namespace Aw.Sindri.Tests.Setup;

/// <summary>The 'setup' built-in service (setup-once spec §5): it runs without a profile entry, so the services list shows it and PauseService refuses it; its tick gives a profile entry the 'repo.added' it is missing.</summary>
public sealed class SetupServiceTests : IDisposable
{
    private readonly AddRepoRig _rig = new();

    public void Dispose() => _rig.Dispose();

    private SetupService Service() => new(_rig.Slice.Profiles, _rig.Backfill);

    private static Task<Kernel.Result<ScheduledResult>> Tick(SetupService service) =>
        service.Run(new ScheduledRun(Grant: default!, DateTimeOffset.UnixEpoch), TestContext.Current.CancellationToken).AsTask();

    [Fact, Trait("Lane", "unit")]
    public void It_is_a_built_in_named_setup_with_a_one_minute_backstop()
    {
        Assert.Equal("setup", SetupService.BuiltIn.Name.Value);
        Assert.Equal(new Core.Profile.Cadence(TimeSpan.FromSeconds(60)), SetupService.BuiltIn.Cadence);
        Assert.Equal(ServiceName.From("setup"), SetupService.ServiceId);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_tick_with_no_profile_is_quiet()
    {
        var service = Service();

        var ran = await Tick(service);

        Assert.True(ran.Value.Idle);
        Assert.Equal(SetupService.ServiceId, service.Identity());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_tick_gives_a_profile_entry_without_repo_added_its_event_once()
    {
        _ = await _rig.Slice.HoldLease();
        _rig.Enter();
        _rig.ProfileNames(("demo", "/work/demo"));
        var service = Service();

        _ = await Tick(service);
        _ = await Tick(service);

        var added = Assert.Single(_rig.Slice.Store.Events, static e => e.Envelope.Type == EventTypes.RepoAdded);
        Assert.Equal("repo.added:demo", added.IdempotencyKey);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_tick_leaves_a_repo_that_has_its_event_alone()
    {
        await _rig.Lease();
        var work = _rig.Service();
        var request = new Features.AddRepo.AddRepoRequest("/work/demo");
        _ = await _rig.Admission().Admit(request, TestContext.Current.CancellationToken);
        _ = await work.Handle(request, TestContext.Current.CancellationToken);
        _rig.ProfileNames(("demo", "/work/demo"));

        _ = await Tick(Service());

        _ = Assert.Single(_rig.Slice.Store.Events, static e => e.Envelope.Type == EventTypes.RepoAdded);
    }
}
