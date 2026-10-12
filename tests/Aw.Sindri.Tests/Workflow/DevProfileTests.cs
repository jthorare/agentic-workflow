using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Profile;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>recipes/dev/profile.yaml (spec §12) stays inside S1's closed profile schema until the workflow keys arrive (P4, P6, P7).</summary>
public sealed class DevProfileTests
{
    [Fact, Trait("Lane", "unit")]
    public void Dev_profile_validates_against_the_profile_schema()
    {
        Assert.Null(YamlToJson.TryConvert(ShippedRecipes.Read("dev/profile.yaml"), out var json));

        var loaded = ProfileReading.FromJson(json, "profile.yaml", ProfileFiles.Options);

        Assert.True(loaded.IsSuccess, loaded.IsSuccess ? null : loaded.Error.Message);
    }
}
