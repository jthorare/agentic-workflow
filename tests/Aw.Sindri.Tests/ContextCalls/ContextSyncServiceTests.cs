using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The 'context-sync' service on a profile with no 'context:' section (#3131), over a stub engine.</summary>
public sealed class ContextSyncServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
    private readonly SliceHarness _h = new();
    private readonly SyncEngineProxy _engine = SyncEngineProxy.Create();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_with_no_context_section_reads_the_sources_as_default_settings_and_says_when_it_has_none()
    {
        _h.Clock.SetUtcNow(_now);
        var loaded = SliceHarness.Profile("5m");
        _ = await _h.Profiles.Save(loaded with { Profile = loaded.Profile with { Context = null } }, Ct);
        var grant = await _h.HoldLease();
        using var caller = _h.Callers.EnterService("context-sync");
        var service = new ContextSyncService(new ContextScopes(_h.Profiles, _h.Callers, new FakeRepoCheckouts()), _engine.Proxy, _h.Store, _h.Events);

        var result = await service.Run(new ScheduledRun(grant, _now), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _engine.SourcesCalls);
        Assert.True(result.Value.Idle);
        Assert.Equal("no context source configured", result.Value.Summary);
    }
}
