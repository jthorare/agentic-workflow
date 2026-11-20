using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.ListServices;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.ListServices;

/// <summary>With the live profile unloadable, the status and GET /services both list what the scheduler runs, so one count shows everywhere (#3623).</summary>
public sealed class ServicesOneCountTests
{
    private readonly ViewsHarness _v = new();
    private readonly LiveSchedule _live = new();
    private readonly List<BuiltInService> _builtIns = [new(ServiceName.From("sync-gaps"), new Cadence(TimeSpan.FromMinutes(1)))];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Status_lists_the_services_the_scheduler_runs_when_the_profile_wont_load()
    {
        _v.Slice.Profiles.Live = SindriErrors.ProfileInvalid.With("line 3: unknown key");
        _live.Publish(Running());

        var status = (await StatusService().Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal([("digest", "5m"), ("sync-gaps", "1m")], status.Services.Select(s => (s.Name.Value, s.Every)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_services_list_keeps_a_profile_service_the_scheduler_still_runs()
    {
        _live.Publish(Running());   // no approved profile reads now, the schedule still holds the profile's 'digest'

        var list = (await ListService().Handle(new ListServicesRequest(), Token)).Value;

        Assert.Equal([("digest", "5m"), ("sync-gaps", "1m")], list.Services.Select(s => (s.Name.Value, s.Every)));
        Assert.Null(list.Empty);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_status_and_the_services_list_give_the_same_names()
    {
        _v.Slice.Profiles.Live = SindriErrors.ProfileInvalid.With("line 3: unknown key");
        _live.Publish(Running());

        var status = (await StatusService().Handle(new GetStatusRequest(), Token)).Value;
        var list = (await ListService().Handle(new ListServicesRequest(), Token)).Value;

        Assert.Equal(list.Services.Select(s => s.Name.Value), status.Services.Select(s => s.Name.Value), StringComparer.Ordinal);
    }

    private IReadOnlyList<ServiceSchedule> Running()
    {
        var now = _v.Slice.Clock.GetUtcNow();
        return [new(ServiceName.From("digest"), new Cadence(TimeSpan.FromMinutes(5)), now), new(ServiceName.From("sync-gaps"), new Cadence(TimeSpan.FromMinutes(1)), now)];
    }

    private ListServicesService ListService() => new(_v.Slice.Profiles, _v.Views, _v.Progress, new InMemoryJobViews(), _builtIns, _live, _v.Slice.Clock);

    private GetStatusService StatusService() => new(_v.Slice.Held, new ProfileReads(_v.Slice.Profiles, _v.Slice.Profiles), _v.Slice.Store, _v.Progress, new HealthServiceRows(_v.Views, []), new InMemorySessionSnapshot(), _live);
}
