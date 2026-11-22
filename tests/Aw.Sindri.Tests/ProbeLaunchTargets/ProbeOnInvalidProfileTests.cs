using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.ProbeLaunchTargets;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.ProbeLaunchTargets;

/// <summary>The launch probe tick on an invalid live profile file (#3594): it keeps probing the targets, and the profile's repos read as none.</summary>
[Lane(TestLane.Unit)]
public sealed class ProbeOnInvalidProfileTests
{
    private readonly SliceHarness _slice = new();
    private readonly FakeLauncher _launcher = new();
    private readonly FakeRepoLocator _repos = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_invalid_profile_file_still_records_the_probe_with_no_repos()
    {
        _ = await _slice.HoldLease();
        _slice.Profiles.Live = SindriErrors.ProfileInvalid.With("/home/thor/profile.yaml: line 3: unknown key 'tennant'");
        _launcher.Probed = _launcher.Probed with
        {
            Targets = [new TargetFacts(AgentId.From("claude-code"), Present: true, "1.0", Limited: null, Problem: null, CatalogSource.Builtin, Overridden: false)],
        };
        var service = new ProbeLaunchTargetsService(_slice.Held, new LaunchProbeRecorder(_launcher, _slice.Profiles, _repos, _slice.Store, _slice.Events));

        var result = await service.Handle(A.ProbeLaunchTargetsRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Code : null);
        var probed = _slice.Events.Payload<LaunchProbed>(Assert.Single(_slice.Store.Events, static e => string.Equals(e.Envelope.Type, EventTypes.LaunchProbed, StringComparison.Ordinal))).Value;
        Assert.Equal(["claude-code"], probed.Targets.Select(static t => t.Id.Value), StringComparer.Ordinal);
        Assert.Empty(probed.Repos);
    }
}
