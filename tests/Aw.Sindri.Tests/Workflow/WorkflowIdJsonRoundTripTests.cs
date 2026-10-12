using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Tests.Workflow;

public sealed class WorkflowIdJsonRoundTripTests
{
    private static readonly JsonSerializerOptions _options = new() { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

    [Fact, Trait("Lane", "unit")]
    public void Workflow_ids_write_as_strings_and_read_back()
    {
        RoundTrips(RecipeId.From("workflow.step"));
        RoundTrips(BlockId.From("gate.budget.available"));
        RoundTrips(FactKey.From("evidence:checks@head"));
        RoundTrips(EventKind.From("item.routed"));
        RoundTrips(OutcomeName.From("alreadySatisfied"));
        RoundTrips(AnswerId.From("scope"));
        RoundTrips(RecipeHash.From("sha256:" + new string('a', 64)));
        RoundTrips(RecipeDuration.From("10m"));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("\"Not An Id\"")]
    [InlineData("\"\"")]
    [InlineData("42")]
    public void A_malformed_workflow_id_is_a_json_error(string json)
    {
        _ = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RecipeId>(json, _options));
        _ = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BlockId>(json, _options));
        _ = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<FactKey>(json, _options));
        _ = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EventKind>(json, _options));
        _ = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OutcomeName>(json, _options));
        _ = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AnswerId>(json, _options));
        _ = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RecipeHash>(json, _options));
        _ = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RecipeDuration>(json, _options));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_fact_key_is_a_dictionary_key()
    {
        var inputs = new Dictionary<FactKey, string> { [FactKey.From("fact:spec")] = "x" };

        var json = JsonSerializer.Serialize(inputs, _options);

        Assert.Equal("{\"fact:spec\":\"x\"}", json);
        Assert.Equal(inputs, JsonSerializer.Deserialize<Dictionary<FactKey, string>>(json, _options));
    }

    private static void RoundTrips<T>(T id)
        where T : struct
    {
        var json = JsonSerializer.Serialize(id, _options);
        Assert.StartsWith("\"", json, StringComparison.Ordinal);
        Assert.Equal(id, JsonSerializer.Deserialize<T>(json, _options));
    }
}
