using System.Text.Json;
using Aw.Context.Testing.Engine;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ValidateProfile;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>'context.sources.onyx' is retired: 'profile validate' still accepts a profile that carries it.</summary>
public sealed class OnyxProfileTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Validate_still_loads_a_profile_that_carries_the_retired_onyx_key()
    {
        _h.Profiles.Live = With("""{"sources":{"onyx":{"url":"http://127.0.0.1:3189","token":"on_live-key-0123","version":"v4.9.0"}}}""");
        var service = new ValidateProfileService(_h.Profiles, _h.Profiles, _h.Store, _h.Events, FakeContextEngine.Instance);

        var validated = await service.Handle(new ValidateProfileRequest(), Token);

        Assert.False(validated.IsError);
    }

    private static LoadedProfile With(string context, IReadOnlyList<RepoEntry>? repos = null)
    {
        var json = SliceHarness.Json;
        var profile = new SindriProfile(1, "thor", Repos: repos, Context: JsonDocument.Parse(context).RootElement.Clone());
        return new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, json)), "/home/profile.yaml");
    }
}
