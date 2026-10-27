using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Models;
using Aw.Kernel;

namespace Aw.Context.Tests.Core;

public sealed class ConceptScanSettingsTests
{
    private static Result<ContextSettings> Read(string json) => SettingsReader.Read(JsonDocument.Parse(json).RootElement.Clone());

    [Fact, Trait("Lane", "unit")]
    public void Without_the_key_the_defaults_stand_and_only_claude_has_model_names()
    {
        var scan = SettingsReader.Read(section: null).Value.ConceptScan;

        Assert.Equal(ConceptScanTiers.Default, scan.Tiers);
        Assert.Equal(new ConceptScanModels(new CliModels("haiku", "sonnet"), new CliModels(Cheap: null, Strong: null), new CliModels(Cheap: null, Strong: null)), scan.Models);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_profile_overrides_the_thresholds_and_any_model_name()
    {
        var scan = Read("""{"conceptScan":{"strongAboveFeatures":40,"strongAbovePromptTokens":5000,"models":{"claude":{"cheap":"claude-haiku-5-5"},"cursor":{"strong":"auto"}}}}""").Value.ConceptScan;

        Assert.Equal(new ConceptScanTiers(40, 5000), scan.Tiers);
        Assert.Equal(new CliModels("claude-haiku-5-5", "sonnet"), scan.Models.Claude);
        Assert.Equal(new CliModels(Cheap: null, Strong: "auto"), scan.Models.Cursor);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"conceptScan":{"strongAboveFeatures":0}}""", "context.conceptScan.strongAboveFeatures")]
    [InlineData("""{"conceptScan":{"strongAbovePromptTokens":-5}}""", "context.conceptScan.strongAbovePromptTokens")]
    [InlineData("""{"conceptScan":{"models":{"codex":{"cheap":""}}}}""", "context.conceptScan.models.codex.cheap")]
    [InlineData("""{"conceptScan":{"models":{"claude":{"strong":"--dangerous"}}}}""", "context.conceptScan.models.claude.strong")]
    [InlineData("""{"conceptScan":{"models":{"gemini":{"cheap":"x"}}}}""", "context")]
    public void A_wrong_value_or_key_is_SND_CONTEXT_001_with_the_key_path_first(string json, string keyPath)
    {
        var error = Read(json).Error;

        Assert.Equal(ContextErrors.SettingsInvalid.Code, error.Code);
        Assert.Contains(keyPath, error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Models_for_a_tier_leave_out_every_cli_with_no_name()
    {
        var models = new ConceptScanModels(new CliModels("a", "b"), new CliModels(Cheap: null, Strong: "c"), new CliModels(Cheap: null, Strong: null));

        Assert.Equal(new Dictionary<string, string>(StringComparer.Ordinal) { ["claude"] = "a" }, models.For(ModelTier.Cheap));
        Assert.Equal(new Dictionary<string, string>(StringComparer.Ordinal) { ["claude"] = "b", ["codex"] = "c" }, models.For(ModelTier.Strong));
    }
}
