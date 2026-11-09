using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Testing.Engine;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ValidateProfile;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>The old 'context.conceptScan.models' key (model routing spec §9, issue 1867): `profile validate` and `profile approve` refuse it with the key that replaced it.</summary>
public sealed class OldScanModelsKeyTests
{
    private const string OldOnly = """{"conceptScan":{"models":{"claude":{"cheap":"old"}}}}""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LoadedProfile Loaded(string context)
    {
        var profile = SliceHarness.Profile().Profile with { Context = JsonDocument.Parse(context).RootElement };
        return new LoadedProfile(profile, ProfileHashing.Of(System.Text.Encoding.UTF8.GetBytes(context)), "/home/profile.yaml");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Validate_refuses_the_old_key_and_names_context_models_names()
    {
        var h = new SliceHarness();
        h.Profiles.Live = Loaded(OldOnly);

        var validated = await new ValidateProfileService(h.Profiles, FakeContextEngine.Instance).Handle(new ValidateProfileRequest(), Token);

        Assert.Equal(ContextErrors.SettingsInvalid.Code, validated.Error.Code);
        Assert.Contains("context.models.names", validated.Error.Message, StringComparison.Ordinal);
    }

}
