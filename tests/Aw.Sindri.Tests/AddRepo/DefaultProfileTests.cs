using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Tests.Profile;

namespace Aw.Sindri.Tests.AddRepo;

/// <summary>
/// The profile the first AddRepo creates (setup-once spec §5.1): it reads back as a valid profile with the user and the effects it was given.
/// </summary>
public sealed class DefaultProfileTests
{
    private static LoadedProfile Load(string yaml)
    {
        Assert.Null(YamlToJson.TryConvert(yaml, out var json));
        var loaded = ProfileReading.FromJson(json, "profile.yaml", ProfileFiles.Options);
        Assert.True(loaded.IsSuccess, loaded.IsSuccess ? null : loaded.Error.Message);
        return loaded.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public void A_new_profile_reads_back_with_the_user_and_the_effects()
    {
        var loaded = Load(DefaultProfile.Text("thor", ["effect.notify.desk", "effect.claim.ref"]));

        Assert.Equal((1, "thor"), (loaded.Profile.SchemaVersion, loaded.Profile.User));
        Assert.Equal(["effect.claim.ref", "effect.notify.desk"], loaded.Profile.Workflow!.Effects);
        Assert.Empty(loaded.Profile.Repos!);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_user_name_with_a_quote_or_a_colon_stays_one_scalar()
        => Assert.Equal("a \"b\": c", Load(DefaultProfile.Text("a \"b\": c", [])).Profile.User);

    [Fact, Trait("Lane", "unit")]
    public void With_no_shipped_effects_the_key_is_an_empty_allowlist_not_absent()
    {
        var loaded = Load(DefaultProfile.Text("thor", []));

        Assert.Empty(loaded.Profile.Workflow!.Effects!);
    }
}
