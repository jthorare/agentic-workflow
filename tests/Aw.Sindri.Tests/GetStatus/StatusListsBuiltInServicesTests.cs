using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.GetStatus;

/// <summary>Bug #3127: Home's health strip said "0 services" while GET /sindri/services listed the built-ins; status lists them too, with or without an approved profile.</summary>
public sealed class StatusListsBuiltInServicesTests
{
    private static readonly BuiltInService[] _builtIns =
    [
        new(ServiceName.From("event-bridge"), Cadence.Parse("1m").Value),
        new(ServiceName.From("expire-memory"), Cadence.Parse("1h").Value),
        new(ServiceName.From("heartbeat"), Cadence.Parse("30m").Value),
    ];

    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GetStatusService Service() => new(_h.Held, new ProfileReads(_h.Profiles, _h.Profiles), _h.Store, new ProjectorProgress(_h.Clock),
        new HealthServiceRows(new InMemoryViewStore(SliceHarness.Json), []), new InMemorySessionSnapshot(), _builtIns);

    [Fact, Trait("Lane", "unit")]
    public async Task Status_lists_every_built_in_when_the_approved_profile_names_no_services()
    {
        _h.Profiles.Live = SliceHarness.ProfileOf("thor");

        var status = (await Service().Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal(["event-bridge", "expire-memory", "heartbeat"], [.. status.Services.Select(s => s.Name.Value).Order(StringComparer.Ordinal)]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_lists_the_built_ins_with_no_approved_profile()
    {
        var status = (await Service().Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal(["event-bridge", "expire-memory", "heartbeat"], [.. status.Services.Select(s => s.Name.Value).Order(StringComparer.Ordinal)]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_lists_a_service_the_profile_names_once_with_the_profiles_cadence()
    {
        _h.Profiles.Live = SliceHarness.Profile("5s");

        var status = (await Service().Handle(new GetStatusRequest(), Token)).Value;

        var heartbeat = Assert.Single(status.Services, s => s.Name.Value == "heartbeat");
        Assert.Equal("5s", heartbeat.Every);
    }
}
