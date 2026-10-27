using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch.Models;

/// <summary>The constraints applied before Jev is asked (model-pick spec §4.1): floor, then cap or pin, then the drafter's model, then the failed model; each removed model is attributed to the first rule that took it.</summary>
[Lane(TestLane.Unit)]
public sealed class ModelCandidatesTests
{
    private static RankedModel Ranked(string id, int order, ModelTier tiers) => new(new ModelFacts(id, id, ModelSource.Catalog, order), tiers, Unordered: false);

    private static readonly IReadOnlyList<RankedModel> _three =
    [
        Ranked("haiku", 0, ModelTier.Cheap),
        Ranked("sonnet", 1, ModelTier.Standard),
        Ranked("opus", 2, ModelTier.Strong),
    ];

    private static string Left(ModelSelection selection) => string.Join(',', selection.Candidates.Select(c => c.Model.Slug));

    private static string Removed(ModelSelection selection) => string.Join(',', selection.Removed.Select(r => $"{r.Model.Model.Slug}:{r.Constraint}"));

    private static readonly Dictionary<string, (ModelConstraints Constraints, string Left, string Removed)> _cases = new(StringComparer.Ordinal)
    {
        { "nothing set keeps every model", (new(), "haiku,sonnet,opus", "") },
        { "no floor given as cheap keeps every model", (new(Floor: ModelTier.Cheap), "haiku,sonnet,opus", "") },
        { "a standard floor drops the cheap model", (new(Floor: ModelTier.Standard), "sonnet,opus", "haiku:Floor") },
        { "a strong floor leaves the strong model", (new(Floor: ModelTier.Strong), "opus", "haiku:Floor,sonnet:Floor") },
        { "a cap drops what is above it", (new(Cap: "sonnet"), "haiku,sonnet", "opus:Cap") },
        { "a cap the agent does not offer is ignored", (new(Cap: "gpt-9"), "haiku,sonnet,opus", "") },
        { "a pin leaves only that model", (new(Pin: "sonnet"), "sonnet", "haiku:Pin,opus:Pin") },
        { "a pin the agent does not offer leaves nothing", (new(Pin: "gpt-9"), "", "haiku:Pin,sonnet:Pin,opus:Pin") },
        { "a floor above the cap leaves nothing", (new(Floor: ModelTier.Strong, Cap: "sonnet"), "", "haiku:Floor,sonnet:Floor,opus:Cap") },
        { "a pin below the floor leaves nothing", (new(Floor: ModelTier.Strong, Pin: "sonnet"), "", "haiku:Floor,sonnet:Floor,opus:Pin") },
        { "a challenger on the drafter's agent skips the drafter's model", (new(Drafter: new(SameAgent: true, Model: "sonnet")), "haiku,opus", "sonnet:Drafter") },
        { "a challenger on another agent keeps every model", (new(Drafter: new(SameAgent: false, Model: "sonnet")), "haiku,sonnet,opus", "") },
        { "a challenger on the drafter's agent with no recorded drafter model leaves nothing", (new(Drafter: new(SameAgent: true, Model: null)), "", "haiku:DrafterUnknown,sonnet:DrafterUnknown,opus:DrafterUnknown") },
        { "an escalation skips the failed model and everything below", (new(Failed: "sonnet"), "opus", "haiku:Failed,sonnet:Failed") },
        { "an escalation from the strongest model leaves nothing", (new(Failed: "opus"), "", "haiku:Failed,sonnet:Failed,opus:Failed") },
        { "an escalation from a model the agent does not offer removes nothing", (new(Failed: "gpt-9"), "haiku,sonnet,opus", "") },
        { "an escalation stays inside the cap", (new(Cap: "sonnet", Failed: "haiku"), "sonnet", "opus:Cap,haiku:Failed") },
        { "the first rule to remove a model gets the blame", (new(Floor: ModelTier.Standard, Failed: "sonnet"), "opus", "haiku:Floor,sonnet:Failed") },
    };

    public static TheoryData<string> Names => [.. _cases.Keys];

    [Theory]
    [MemberData(nameof(Names))]
    public void The_constraints_leave_these_candidates(string name)
    {
        var (constraints, left, removed) = _cases[name];
        var selection = ModelCandidates.For(_three, constraints);

        Expect.Equal((left, removed), (Left(selection), Removed(selection)), name);
    }

    [Fact]
    public void An_agent_with_one_model_clears_any_floor()
    {
        var only = new[] { Ranked("solo", 0, ModelTier.Cheap | ModelTier.Standard | ModelTier.Strong) };

        var selection = ModelCandidates.For(only, new ModelConstraints(Floor: ModelTier.Strong));

        Expect.Equal(("solo", string.Empty), (Left(selection), Removed(selection)));
    }

    [Fact]
    public void An_agent_with_no_models_has_no_candidates_and_nothing_removed()
    {
        var selection = ModelCandidates.For([], new ModelConstraints(Floor: ModelTier.Strong));

        Expect.Equal((string.Empty, string.Empty), (Left(selection), Removed(selection)));
    }
}
