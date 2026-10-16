using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Engine.ChildFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>A child created in its parent's Commit, and what its park, resume or escalation does to the parent (spec §5.7).</summary>
public sealed class ChildPropagationTests
{
    private static InMemoryChildRecipes Pinned => new InMemoryChildRecipes().With(new ChildRecipe(StepRecipe, StepPin));

    [Fact, Trait("Lane", "unit")]
    public void Create_derives_the_child_from_the_parent_pin_and_address()
    {
        var start = ChildPropagation.Create(Waiting, Wait, Pinned, Terminal).Value.Request;

        Assert.Equal(InstanceAddress.From("T-3/item/1/step.4"), start.Address);
        Assert.Equal(TriggerKey.For(start.Id, "start"), start.Key);
        Assert.Equal(Step, start.Recipe.Id);
        Assert.Equal(StepRecipe.Version, start.Recipe.Version);
        Assert.Equal(StepPin, start.Recipe.Hash);
        Assert.Empty(start.Recipe.Children);
        Assert.Equal(Waiting.Item, start.Item);
        Assert.Equal(Waiting.Attempt, start.Attempt);
        Assert.Equal(ParentId, start.Parent);
        Assert.Equal(Terminal, start.Actor);
        Assert.False(start.TakenOver);
        Assert.Same(StepRecipe, ChildPropagation.Create(Waiting, Wait, Pinned, Terminal).Value.Recipe);
    }

    [Fact, Trait("Lane", "unit")]
    public void Create_hashes_the_child_address_into_a_version_8_id()
    {
        var start = ChildPropagation.Create(Waiting, Wait, Pinned, Terminal).Value.Request;

        // §5.1: the child's workflow id is its address; its opaque id is derived from it, so the same child gets the same id.
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes("T-3/item/1/step.4"));
        var hex = start.Id.Value["WFI-".Length..];
        Assert.Equal(Convert.ToHexStringLower(digest)[..12], hex[..12]);
        Assert.Equal('8', hex[12]);
        Assert.Contains(hex[16], "89ab");
        Assert.Equal(start.Id, ChildPropagation.Create(Waiting, Wait, Pinned, Terminal).Value.Request.Id);
        var nextEntry = Waiting with { Frame = Waiting.Frame with { Entry = Entry + 1 } };
        Assert.NotEqual(start.Id, ChildPropagation.Create(nextEntry, Wait, Pinned, Terminal).Value.Request.Id);
    }

    [Fact, Trait("Lane", "unit")]
    public void Create_refuses_a_child_the_parent_does_not_pin_or_the_resolver_lacks()
    {
        var unpinned = Waiting with { Recipe = Waiting.Recipe with { Children = new Dictionary<RecipeId, RecipeHash>() } };

        Assert.Equal("SND-RECIPE-002", ChildPropagation.Create(unpinned, Wait, Pinned, Terminal).Error.Code);
        Assert.Equal("SND-RECIPE-002", ChildPropagation.Create(Waiting, Wait, InMemoryChildRecipes.None, Terminal).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Create_refuses_a_child_whose_resolved_hash_is_not_the_one_the_parent_pinned()
    {
        var other = new InMemoryChildRecipes().With(new ChildRecipe(StepRecipe, RecipeHash.From("sha256:" + new string('6', 64))));

        var refused = ChildPropagation.Create(Waiting, Wait, other, Terminal);

        Assert.Equal("SND-RECIPE-002", refused.Error.Code);
        Assert.Contains(StepPin.Value, refused.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Create_copies_a_fact_input_from_the_parent()
    {
        var start = ChildPropagation.Create(Waiting, Wait, Pinned, Terminal).Value.Request;

        Assert.Equal(StepSpecValue, Assert.Single(start.Inputs, i => i.Key == Spec).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void Create_records_a_literal_input_by_its_json_and_skips_a_profile_input()
    {
        var limit = FactKey.From("fact:limit");
        var profiled = FactKey.From("fact:model");
        var wait = Wait with
        {
            Inputs = new Dictionary<FactKey, WithValue>
            {
                [limit] = Literal("""{ "rounds": 3 }"""),
                [profiled] = new(Literal: null, Fact: null, Profile: "models.default"),
            },
        };

        var start = ChildPropagation.Create(Waiting, wait, Pinned, Terminal).Value.Request;

        var value = Assert.Single(start.Inputs).Value;
        Assert.Equal("literal:{\"rounds\":3}", value.Ref);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("{\"rounds\":3}"))), value.Sha256);
        Assert.Null(value.HeadSha);
        Assert.False(start.Inputs.ContainsKey(profiled));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_child_park_mirrors_its_reason_and_question_with_propagate()
    {
        var effect = ChildPropagation.ToParent(Waiting, Item, Changed(ChildChange.Parked, ParkReason.NoProgress, "checks failed twice"));

        Assert.Equal(ParentEffectKind.MirrorPark, effect.Kind);
        var suspend = Assert.IsType<Suspension>(effect.Suspend);
        Assert.Equal(SuspensionKind.Park, suspend.Kind);
        Assert.Equal(ParkReason.NoProgress, suspend.Reason);
        Assert.Null(suspend.ParkKind);
        Assert.Equal("checks failed twice", suspend.Question);
        Assert.Equal(ChildId, suspend.Mirrors);
        Assert.Equal(ResumeKind.Forward, suspend.Resume.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_child_park_is_ignored_with_onParked_ignore()
    {
        var effect = ChildPropagation.ToParent(Waiting, ItemWith(OnParked.Ignore), Changed(ChildChange.Parked, ParkReason.Budget, "budget spent"));

        Assert.Equal(ParentEffectKind.Ignore, effect.Kind);
        Assert.Null(effect.Suspend);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(OnParked.Propagate)]
    [InlineData(OnParked.Ignore)]
    public void A_child_escalation_always_escalates_the_parent(OnParked onParked)
    {
        var effect = ChildPropagation.ToParent(Waiting, ItemWith(onParked), Changed(ChildChange.Escalated, question: "gate.verify.judge scored 0.62"));

        Assert.Equal(ParentEffectKind.Escalate, effect.Kind);
        var suspend = Assert.IsType<Suspension>(effect.Suspend);
        Assert.Equal(SuspensionKind.Escalation, suspend.Kind);
        Assert.Equal(EscalationKind.Child, suspend.Escalation);
        Assert.Equal("gate.verify.judge scored 0.62", suspend.Question);
        Assert.Equal(ChildId, suspend.Mirrors);
        Assert.Equal(ResumeKind.Forward, suspend.Resume.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_child_resume_releases_the_suspension_mirroring_it()
    {
        Assert.Equal(ParentEffectKind.Release, ChildPropagation.ToParent(MirroringPark, Item, Changed(ChildChange.Resumed)).Kind);
        Assert.Equal(ParentEffectKind.Release, ChildPropagation.ToParent(MirroringEscalation, Item, Changed(ChildChange.Resumed)).Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_child_resume_with_no_mirror_is_ignored() =>
        Assert.Equal(ParentEffectKind.Ignore, ChildPropagation.ToParent(Waiting, Item, Changed(ChildChange.Resumed)).Kind);

    [Theory, Trait("Lane", "unit")]
    [InlineData(ChildChange.Parked)]
    [InlineData(ChildChange.Escalated)]
    [InlineData(ChildChange.Resumed)]
    public void A_taken_over_parent_holds_the_row(ChildChange change)
    {
        var effect = ChildPropagation.ToParent(TakenOver(MirroringPark), Item, Changed(change, ParkReason.Rounds, "rounds spent"));

        Assert.Equal(ParentEffectKind.Hold, effect.Kind);
        Assert.Null(effect.Suspend);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_parent_parked_for_its_own_reason_holds_the_row()
    {
        var parked = Push(Waiting, new Suspension(
            SuspensionKind.Park, ParkReason.Timeout, ParkKind: null, Escalation: null, "running timed out", "service:workflow.timers", Mirrors: null,
            new ResumeAction(ResumeKind.Rearm, Withheld: null, Gate: null, Effects: [])));

        Assert.Equal(ParentEffectKind.Hold, ChildPropagation.ToParent(parked, Item, Changed(ChildChange.Escalated, question: "q")).Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_row_from_a_child_the_parent_no_longer_waits_on_is_ignored() =>
        Assert.Equal(ParentEffectKind.Ignore, ChildPropagation.ToParent(Waiting, Item, Changed(ChildChange.Escalated, question: "q", child: OtherChildId)).Kind);

    [Fact, Trait("Lane", "unit")]
    public void A_row_for_an_ended_parent_is_ignored()
    {
        var ended = Waiting with { Frame = Waiting.Frame with { State = "done", Child = null }, Outcome = OutcomeName.From("resolved") };

        Assert.Equal(ParentEffectKind.Ignore, ChildPropagation.ToParent(ended, Item, Changed(ChildChange.Parked, ParkReason.Rounds, "q")).Kind);
    }
}
