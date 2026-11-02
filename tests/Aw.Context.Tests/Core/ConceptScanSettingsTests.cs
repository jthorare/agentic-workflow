using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Tests.Core;

public sealed class ConceptScanSettingsTests
{
    private static Result<ContextSettings> Read(string json) => SettingsReader.Read(JsonDocument.Parse(json).RootElement.Clone());

    [Fact, Trait("Lane", "unit")]
    public void Without_the_key_the_default_thresholds_stand() =>
        Assert.Equal(ConceptScanTiers.Default, SettingsReader.Read(section: null).Value.ConceptScan.Tiers);

    [Fact, Trait("Lane", "unit")]
    public void The_profile_overrides_the_thresholds() =>
        Assert.Equal(new ConceptScanTiers(40, 5000), Read("""{"conceptScan":{"strongAboveFeatures":40,"strongAbovePromptTokens":5000}}""").Value.ConceptScan.Tiers);

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"conceptScan":{"strongAboveFeatures":0}}""", "context.conceptScan.strongAboveFeatures")]
    [InlineData("""{"conceptScan":{"strongAbovePromptTokens":-5}}""", "context.conceptScan.strongAbovePromptTokens")]
    [InlineData("""{"conceptScan":{"tier":"strong"}}""", "context")]
    public void A_wrong_value_or_key_is_SND_CONTEXT_001_with_the_key_path_first(string json, string keyPath)
    {
        var error = Read(json).Error;

        Assert.Equal(ContextErrors.SettingsInvalid.Code, error.Code);
        Assert.Contains(keyPath, error.Message, StringComparison.Ordinal);
    }

    // Issue 1867 (model routing spec §9): the old key was read one release after context.models.names shipped; now it is refused like any unknown key, with the key to move it to.
    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"conceptScan":{"models":{"claude":{"cheap":"claude-haiku-5-5"}}}}""")]
    [InlineData("""{"conceptScan":{"models":{}}}""")]
    [InlineData("""{"conceptScan":{"strongAboveFeatures":40,"models":{"codex":{"strong":"codex-big"}}},"models":{"names":{"codex":{"strong":"codex-big"}}}}""")]
    public void The_old_models_key_is_refused_and_names_the_key_that_replaced_it(string json)
    {
        var error = Read(json).Error;

        Assert.Equal(ContextErrors.SettingsInvalid.Code, error.Code);
        Assert.StartsWith("context.conceptScan.models:", error.Message, StringComparison.Ordinal);
        Assert.Contains("context.models.names", error.Message, StringComparison.Ordinal);
    }
}
