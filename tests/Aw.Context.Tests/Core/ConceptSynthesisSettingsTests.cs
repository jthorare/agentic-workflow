using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Tests.Core;

public sealed class ConceptSynthesisSettingsTests
{
    private static Result<ContextSettings> Read(string json) => SettingsReader.Read(JsonDocument.Parse(json).RootElement.Clone());

    [Fact, Trait("Lane", "unit")]
    public void Without_the_key_the_defaults_stand()
    {
        var synthesis = SettingsReader.Read(section: null).Value.ConceptSynthesis;

        Assert.Equal(new ConceptSynthesisSettings(), synthesis);
        Assert.Equal((true, 8, 0.85f, 20, 2, 1, 1_500, 30_000), (synthesis.Enabled, synthesis.Neighbours, synthesis.MinCosine, synthesis.MaxGroup, synthesis.MinRecords, synthesis.MinSources, synthesis.MaxCharsPerRecord, synthesis.MaxCharsPerCall));
        Assert.Equal(new GroupingSettings(), synthesis.Grouping);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_profile_overrides_any_knob()
    {
        var synthesis = Read("""{"conceptSynthesis":{"enabled":false,"neighbours":12,"minCosine":0.8,"minSources":2}}""").Value.ConceptSynthesis;

        Assert.Equal((false, 12, 0.8f, 2), (synthesis.Enabled, synthesis.Neighbours, synthesis.MinCosine, synthesis.MinSources));
        Assert.Equal(20, synthesis.MaxGroup);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"conceptSynthesis":{"neighbours":0}}""", "context.conceptSynthesis.neighbours")]
    [InlineData("""{"conceptSynthesis":{"minCosine":1.5}}""", "context.conceptSynthesis.minCosine")]
    [InlineData("""{"conceptSynthesis":{"maxGroup":1}}""", "context.conceptSynthesis.maxGroup")]
    [InlineData("""{"conceptSynthesis":{"minRecords":1}}""", "context.conceptSynthesis.minRecords")]
    [InlineData("""{"conceptSynthesis":{"maxCharsPerCall":10}}""", "context.conceptSynthesis.maxCharsPerCall")]
    [InlineData("""{"conceptSynthesis":{"model":"x"}}""", "context")]
    public void A_wrong_value_or_key_is_SND_CONTEXT_001_with_the_key_path_first(string json, string keyPath)
    {
        var error = Read(json).Error;

        Assert.Equal(ContextErrors.SettingsInvalid.Code, error.Code);
        Assert.Contains(keyPath, error.Message, StringComparison.Ordinal);
    }
}
