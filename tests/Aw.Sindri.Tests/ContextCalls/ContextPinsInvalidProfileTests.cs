using Aw.Sindri.Core;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The pins read on an invalid live profile file (#3594): it refuses on purpose, with the SND-PROFILE-002 that names the file to fix, never an empty pin list.</summary>
public sealed class ContextPinsInvalidProfileTests
{
    private const string Invalid = "/home/thor/profile.yaml: line 3: unknown key 'tennant'";
    private readonly SliceHarness _h = new();
    private readonly InMemoryJobViews _jobs = new();
    private readonly HeadsEngineProxy _engine = HeadsEngineProxy.Create();

    [Fact, Trait("Lane", "unit")]
    public async Task An_invalid_profile_file_is_refused_with_the_error_that_names_the_file()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = await _h.Profiles.Save(SliceHarness.Profile("5s"), ct);
        _h.Profiles.Live = SindriErrors.ProfileInvalid.With(Invalid);
        var scopes = new ContextScopes(_h.Profiles, _h.Callers, new FakeRepoCheckouts());
        var service = new GetContextPinsService(scopes, _engine.Proxy, _h.Profiles, _h.Profiles, new MapRebuildState(scopes, () => _engine.Proxy, _jobs, _h.Clock), _jobs);

        var pins = await service.Handle(new GetContextPinsRequest(Repo: null), ct);

        Assert.True(pins.IsError);
        Assert.Equal(SindriErrors.ProfileInvalid.Code, pins.Error.Code);
        Assert.Contains("/home/thor/profile.yaml", pins.Error.Message, StringComparison.Ordinal);
    }
}
