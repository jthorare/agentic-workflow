using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Tests.Ranker;

public sealed class RankerSettingsTests
{
    private static Result<ContextSettings> Read(string json) => SettingsReader.Read(JsonDocument.Parse(json).RootElement.Clone());

    [Fact, Trait("Lane", "unit")]
    public void Both_stages_are_off_by_default_with_equal_weights_and_a_top_of_30()
    {
        foreach (var settings in new[] { SettingsReader.Read(section: null).Value.Ranker, Read("""{"ranker":{}}""").Value.Ranker, Read("""{"deadline":"4s"}""").Value.Ranker })
        {
            Assert.False(settings.Features);
            Assert.False(settings.Rerank);
            Assert.Equal(30, settings.RerankTop);
            Assert.Equal([1d, 1, 1, 1, 1], [settings.Weights.CascadeDistance, settings.Weights.EdgeType, settings.Weights.Provenance, settings.Weights.Supersession, settings.Weights.Recency]);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_profile_turns_the_stages_on_and_sets_the_weights_and_the_list_size()
    {
        var settings = Read("""{"ranker":{"features":true,"rerank":true,"rerankTop":10,"weights":{"recency":2.5,"cascadeDistance":0}}}""").Value.Ranker;

        Assert.True(settings.Features);
        Assert.True(settings.Rerank);
        Assert.Equal(10, settings.RerankTop);
        Assert.Equal((2.5, 0d, 1d), (settings.Weights.Recency, settings.Weights.CascadeDistance, settings.Weights.EdgeType));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"ranker":{"weights":{"recency":-1}}}""", "context.ranker.weights.recency")]
    [InlineData("""{"ranker":{"weights":{"edgeType":10.5}}}""", "context.ranker.weights.edgeType")]
    [InlineData("""{"ranker":{"rerankTop":0}}""", "context.ranker.rerankTop")]
    [InlineData("""{"ranker":{"rerankTop":51}}""", "context.ranker.rerankTop")]
    public void A_value_out_of_range_is_SND_CONTEXT_001_with_the_key_path(string json, string path)
    {
        var settings = Read(json);

        Assert.Equal(ContextErrors.SettingsInvalid.Code, settings.Error.Code);
        Assert.StartsWith(path + ":", settings.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"ranker":{"rerank":"yes"}}""")]
    [InlineData("""{"ranker":{"features":1}}""")]
    [InlineData("""{"ranker":{"rerankTop":1.5}}""")]
    [InlineData("""{"ranker":{"weights":{"novelty":1}}}""")]
    [InlineData("""{"ranker":{"model":"x"}}""")]
    public void A_wrong_type_or_an_unknown_key_is_SND_CONTEXT_001(string json) => Assert.Equal(ContextErrors.SettingsInvalid.Code, Read(json).Error.Code);
}
