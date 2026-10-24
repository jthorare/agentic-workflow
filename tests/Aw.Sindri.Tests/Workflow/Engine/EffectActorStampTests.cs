using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>The engine stamps the trigger's actor into a recipe effect's 'with:' as 'actor', when the effect's descriptor declares that parameter and the recipe sets none.</summary>
public sealed class EffectActorStampTests
{
    private const string Inbox = "service:workflow.inbox";
    private static readonly BlockId _merge = BlockId.From("effect.pr.merge");
    private static readonly BlockId _mergeDocs = BlockId.From("effect.pr.merge-docs");
    private static readonly BlockId _claim = BlockId.From("effect.claim.ref");

    [Fact, Trait("Lane", "unit")]
    public void An_effect_whose_descriptor_declares_actor_gets_the_triggers_actor()
    {
        var applied = Emit(new BlockUse(_merge, Empty()), new BlockUse(_mergeDocs, Empty()));

        Assert.All(applied.Effects, e => Assert.Equal(Inbox, e.With["actor"].Literal!.Value.GetString()));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_that_sets_actor_keeps_its_own()
    {
        var own = new Dictionary<string, WithValue>(StringComparer.Ordinal) { ["actor"] = new(JsonSerializer.SerializeToElement("human:owner"), Fact: null, Profile: null) };

        var applied = Emit(new BlockUse(_merge, own));

        Assert.Equal("human:owner", Assert.Single(applied.Effects).With["actor"].Literal!.Value.GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public void An_effect_without_an_actor_parameter_is_left_as_the_recipe_wrote_it()
    {
        var applied = Emit(new BlockUse(_claim, Empty()));

        Assert.DoesNotContain("actor", Assert.Single(applied.Effects).With.Keys, StringComparer.Ordinal);
    }

    private static Dictionary<string, WithValue> Empty() => new(StringComparer.Ordinal);

    private static Applied Emit(params BlockUse[] effects)
    {
        var recipe = Item with
        {
            Transitions = [.. Item.Transitions.Select(t => t.From.Contains("startable", StringComparer.Ordinal) && string.Equals(t.To, "claimed", StringComparison.Ordinal) ? t with { Effects = effects } : t)],
        };
        var trigger = new WaitSatisfied(Inbox, new ResourceWaitSource(ResourceKind.Slot, 3), Outcome: null, new Dictionary<FactKey, FactValue>());
        return Assert.IsType<Applied>(Decide(At(recipe, "startable", entry: 2), recipe, trigger, Gates(("gate.budget.available", GateVerdict.Pass, null))));
    }
}
