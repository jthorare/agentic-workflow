using System.Text.Json;
using Aw.Context.Testing.Engine;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ValidateProfile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>'context.sources.onyx' as 'profile validate' judges it, before any call to Onyx.</summary>
public sealed class OnyxProfileTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Validate_refuses_an_onyx_key_written_into_the_profile_without_repeating_it()
    {
        _h.Profiles.Live = With("""{"sources":{"onyx":{"url":"http://127.0.0.1:3189","token":"on_live-key-0123","version":"v4.9.0"}}}""");
        var service = new ValidateProfileService(_h.Profiles, _h.Profiles, _h.Store, _h.Events, FakeContextEngine.Instance);

        var validated = await service.Handle(new ValidateProfileRequest(), Token);

        Assert.Equal("SND-CONTEXT-002", validated.Error.Code);
        Assert.StartsWith("context.sources.onyx.token:", validated.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("on_live-key-0123", validated.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Validate_reports_an_onyx_url_off_loopback_with_its_key_path()
    {
        _h.Profiles.Live = With("""{"sources":{"onyx":{"url":"http://onyx.example:3189","token":"env:ONYX_KEY","version":"v4.9.0"}}}""");
        var service = new ValidateProfileService(_h.Profiles, _h.Profiles, _h.Store, _h.Events, FakeContextEngine.Instance);

        var validated = await service.Handle(new ValidateProfileRequest(), Token);

        Assert.Equal("SND-CONTEXT-055", validated.Error.Code);
        Assert.StartsWith("context.sources.onyx.url:", validated.Error.Message, StringComparison.Ordinal);
    }

    private static LoadedProfile With(string context, IReadOnlyList<RepoEntry>? repos = null)
    {
        var json = SliceHarness.Json;
        var profile = new SindriProfile(1, "thor", Repos: repos, Context: JsonDocument.Parse(context).RootElement.Clone());
        return new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, json)), "/home/profile.yaml");
    }
}
