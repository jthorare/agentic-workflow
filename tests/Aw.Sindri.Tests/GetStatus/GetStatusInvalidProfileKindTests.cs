using Aw.Sindri.Core;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.RecordHeartbeat;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.GetStatus;

/// <summary>An unknown key in the live profile gives a normal status that names the profile 'invalid' (#3428).</summary>
public sealed class GetStatusInvalidProfileKindTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task GetStatus_names_an_invalid_live_profile_as_invalid_and_still_answers()
    {
        var grant = await _h.HoldLease();
        _ = await new RecordHeartbeatService(_h.Store, _h.Events, _h.Clock).Handle(new RecordHeartbeatRequest(new ScheduledRun(grant, _h.Clock.GetUtcNow())), Token);
        _h.Profiles.Live = SindriErrors.ProfileInvalid.With("/home/thor/profile.yaml: line 3: unknown key 'tennant'");
        var service = new GetStatusService(_h.Held, new ProfileReads(_h.Profiles, _h.Profiles), _h.Store, new ProjectorProgress(_h.Clock), new HealthServiceRows(new InMemoryViewStore(SliceHarness.Json), []), new InMemorySessionSnapshot());

        var result = await service.Handle(new GetStatusRequest(), Token);

        Assert.False(result.IsError);
        var status = result.Value;
        Assert.Equal("invalid", status.Profile.ProblemKind);
        Assert.Contains("unknown key 'tennant'", status.Profile.Problem, StringComparison.Ordinal);
        Assert.True(status.Running);
        Assert.Equal(grant.Epoch, status.Epoch);
        Assert.NotNull(status.Health);
        Assert.NotNull(status.Views);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetStatus_counts_a_profile_that_wont_load_as_a_health_problem()
    {
        _ = await _h.HoldLease();
        _h.Profiles.Live = SindriErrors.ProfileInvalid.With("The profile file is there but can't be read; check its permissions.");
        var service = new GetStatusService(_h.Held, new ProfileReads(_h.Profiles, _h.Profiles), _h.Store, new ProjectorProgress(_h.Clock), new HealthServiceRows(new InMemoryViewStore(SliceHarness.Json), []), new InMemorySessionSnapshot());

        var health = (await service.Handle(new GetStatusRequest(), Token)).Value.Health;

        Assert.NotNull(health);
        Assert.NotEqual(HealthWord.Healthy, health.Word);
        Assert.Contains(health.Problems, static p => p.Code == SindriErrors.ProfileInvalid.Code);
        Assert.Contains("your profile won't load", health.Sentence, StringComparison.Ordinal);
    }
}
