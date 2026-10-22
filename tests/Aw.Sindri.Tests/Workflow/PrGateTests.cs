using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Gate;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>'gate.pr.autofix-cap' (IDEA-016) and 'gate.revision-bound' (IDEA-009): computed from the instance's facts, no forge.</summary>
public sealed class PrGateTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"ciFixes":0}""", 3)]
    [InlineData("""{"ciFixes":2}""", 3)]
    [InlineData("""{}""", 3)]
    public async Task Autofix_cap_passes_below_the_cap(string push, int cap)
    {
        var result = await Cap(push, cap.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(GateVerdict.Pass, result.Verdict);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"ciFixes":3}""", "3")]
    [InlineData("""{"ciFixes":5}""", "3")]
    public async Task Autofix_cap_refuses_at_the_cap(string push, string cap)
    {
        var result = await Cap(push, cap);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains("3", Assert.Single(result.Reasons), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Autofix_cap_defaults_to_three_when_no_cap_is_given()
    {
        var result = await Cap("""{"ciFixes":3}""", cap: null);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Revision_bound_passes_a_green_unblocked_snapshot()
    {
        var result = await Bound("""{"state":"green","head":{"sha":"abc"},"mergeBlockedBy":[]}""");

        Assert.Equal(GateVerdict.Pass, result.Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Revision_bound_passes_when_no_blockers_are_listed()
    {
        var result = await Bound("""{"state":"green","head":{"sha":"abc"}}""");

        Assert.Equal(GateVerdict.Pass, result.Verdict);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"state":"ciRed","head":{"sha":"abc"}}""", "ciRed")]
    [InlineData("""{"state":"green","mergeBlockedBy":["reviewRequired"]}""", "reviewRequired")]
    [InlineData("""{"state":"green","mergeBlockedBy":["staleProof","reviewRequired"]}""", "staleProof")]
    [InlineData("""{"state":"green","heldBlocksMerge":true,"mergeBlockedBy":["heldComments"]}""", "heldComments")]
    public async Task Revision_bound_refuses_and_names_why(string snapshot, string reason)
    {
        var result = await Bound(snapshot);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains(reason, string.Join(' ', result.Reasons), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Revision_bound_ignores_held_comments_unless_they_block_the_merge()
    {
        var result = await Bound("""{"state":"green","mergeBlockedBy":["heldComments"]}""");

        Assert.Equal(GateVerdict.Pass, result.Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Revision_bound_refuses_without_a_snapshot()
    {
        var result = await new RevisionBoundGate().Evaluate(
            new GateContext(RevisionBoundGate.Descriptor.Id, new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>(), Threshold: null),
            TestContext.Current.CancellationToken);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
    }

    private static async Task<GateResult> Cap(string push, string? cap)
    {
        var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (cap is not null)
        {
            arguments["cap"] = JsonDocument.Parse(cap).RootElement.Clone();
        }

        var facts = new Dictionary<FactKey, JsonElement> { [FactKey.From("fact:push")] = JsonDocument.Parse(push).RootElement.Clone() };
        return await new PrAutofixCapGate().Evaluate(new GateContext(PrAutofixCapGate.Descriptor.Id, arguments, facts, Threshold: null), TestContext.Current.CancellationToken);
    }

    private static async Task<GateResult> Bound(string snapshot)
    {
        var at = JsonDocument.Parse(snapshot).RootElement.Clone();
        var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["at"] = at };
        return await new RevisionBoundGate().Evaluate(new GateContext(RevisionBoundGate.Descriptor.Id, arguments, new Dictionary<FactKey, JsonElement>(), Threshold: null), TestContext.Current.CancellationToken);
    }
}
