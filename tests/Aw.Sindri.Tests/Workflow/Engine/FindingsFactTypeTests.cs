using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;
using EngineFactTypes = Aw.Sindri.Core.Workflow.Engine.Runtime.FactTypes;
using RecipeFactTypes = Aw.Sindri.Core.Workflow.Recipes.FactTypes;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>The 'findings' fact type (model-check design §4.3, M-3u): a recipe may declare it, the view serves it by name, and a viewer in the app picks it up.</summary>
[Lane(TestLane.Unit)]
public sealed class FindingsFactTypeTests
{
    [Fact]
    public void A_recipe_may_declare_findings()
    {
        Assert.True(RecipeFactTypes.IsKnown("findings"));
        Assert.Contains("findings", RecipeFactTypes.All, StringComparer.Ordinal);
    }

    [Fact]
    public void Findings_is_served_as_findings_not_payload()
    {
        Assert.Equal(FactType.Findings, EngineFactTypes.Parse("findings"));
        Assert.Equal("findings", EngineFactTypes.Of("findings"));
        Assert.Equal("findings", FactTypeJsonConverter.Word(FactType.Findings));
    }

    [Fact]
    public void A_type_this_build_does_not_know_is_still_payload()
    {
        Assert.Equal(FactType.Payload, EngineFactTypes.Parse("hologram"));
        Assert.Equal(FactType.Payload, EngineFactTypes.Parse(null));
    }

    [Fact]
    public void A_produced_findings_fact_gets_its_key_handle_like_a_payload_does()
    {
        var recipe = RecipeReaderTests.Bind(RecipeReaderTests.Minimal + "outputs: [{ key: fact:output, label: Findings, type: findings, from: produce }]\n");
        var instance = EngineFixture.At(EngineFixture.Step, "done") with
        {
            Facts = new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = EngineFixture.Fact("[]") },
        };

        var row = Assert.Single(FactRows.Of(instance, GetInstance.StatusCases.Recipe with { Recipe = recipe }, task: null, new Dictionary<FactKey, FactProvenance>()));

        Assert.Equal((FactType.Findings, FactStatus.Produced, "fact:output"), (row.Type, row.Status, row.Ref?.FactKey));
    }
}
