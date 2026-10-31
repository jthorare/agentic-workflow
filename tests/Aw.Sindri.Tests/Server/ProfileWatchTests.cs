using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.CheckProfile;
using Aw.Sindri.Host.Server;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Server;

/// <summary>The profile watch on the fake clock: a check at start and every 30 s while the lease is held, resumed from the ledger.</summary>
public sealed class ProfileWatchTests
{
    private readonly SliceHarness _h = new();
    private readonly ListLogger<ProfileRecheck> _log = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Profile_watch_checks_at_start_and_sees_a_drift_within_30_seconds()
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile("30s"), Token);
        _h.Profiles.Live = SliceHarness.Profile("30s");
        _ = await _h.HoldLease();

        await Run(() => _h.Profiles.Live = SliceHarness.Profile("5s"), ProfileWatch.Every, steps: 1);

        Assert.Equal([false, true], Checks().Select(c => c.Drifted));
        Assert.All(_h.Store.Events.Where(e => e.Envelope.Type == EventTypes.ProfileChecked), e => Assert.Equal("service:profile-watch", e.Envelope.Actor));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Profile_watch_resumes_from_the_last_check_after_a_restart()
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile("30s"), Token);
        _h.Profiles.Live = SliceHarness.Profile("30s");
        _ = await _h.HoldLease();

        await Run();
        await Run();   // a new process: same pair, nothing new

        _ = Assert.Single(Checks());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Nothing_is_checked_without_the_lease()
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile("30s"), Token);
        _h.Profiles.Live = SliceHarness.Profile("30s");

        await Run(between: null, TimeSpan.FromSeconds(1), steps: 3);

        Assert.Empty(Checks());
    }

    /// <summary>Starts a watch and lets its first pass run; then, <paramref name="steps"/> times, runs <paramref name="between"/> and steps the clock by <paramref name="by"/>.</summary>
    private async Task Run(Action? between = null, TimeSpan by = default, int steps = 0)
    {
        await using var provider = new ServiceCollection()
            .AddSingleton<IProfileSource>(_h.Profiles)
            .AddSingleton<IApprovedProfile>(_h.Profiles)
            .AddSingleton<ILedgerStore>(_h.Store)
            .AddSingleton(_h.Events)
            .AddSingleton(_h.Callers)
            .AddSingleton<IIdGenerator>(_h.Ids)
            .AddScoped<IService<CheckProfileRequest, CheckProfileResponse>, CheckProfileService>()
            .BuildServiceProvider();
        var clock = new SteppedClock(_h.Clock);
        using var watch = new ProfileWatch(new ProfileRecheck(provider.GetRequiredService<IServiceScopeFactory>(), _h.Held, _h.Callers, _log), _h.Held, clock);
        await watch.StartAsync(Token);
        await clock.Armed();
        for (var i = 0; i < steps; i++)
        {
            between?.Invoke();
            await clock.Step(by);
            await clock.Armed();
        }

        await watch.StopAsync(Token);
    }

    private List<ProfileChecked> Checks() => [.. _h.Store.Events
        .Where(e => e.Envelope.Type == EventTypes.ProfileChecked)
        .Select(e => _h.Events.Payload<ProfileChecked>(e).Value)];
}
