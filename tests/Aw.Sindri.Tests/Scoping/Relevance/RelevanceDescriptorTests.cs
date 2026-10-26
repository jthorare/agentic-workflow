using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;

namespace Aw.Sindri.Tests.Scoping.Relevance;

/// <summary>What the three relevance Steps read, write and take (spec 4.1 to 4.3): the contract the recipe and the other tasks wire against.</summary>
[Lane(TestLane.Unit)]
public sealed class RelevanceDescriptorTests
{
    private static string[] Reads(BlockDescriptor descriptor) => [.. descriptor.Inputs.Select(static i => i.Value)];

    private static string[] Writes(BlockDescriptor descriptor, string outcome) =>
        [.. descriptor.Outputs.Where(o => o.Every == true || o.RequiredOn.Any(r => r.Value == outcome)).Select(static o => o.Fact.Value).Order(StringComparer.Ordinal)];

    [Fact]
    public void The_review_agent_reads_exactly_the_brief_the_lenses_the_coverage_and_the_anchors()
    {
        var review = RelevanceSteps.Review;

        Assert.Equal("step.scope.relevance-review", review.Id.Value);
        Assert.Equal(BlockRuntime.Agent, review.Runtime);
        Assert.Equal(["fact:brief", "fact:lenses", "fact:coverage", "fact:anchors"], Reads(review), StringComparer.Ordinal);
        Assert.Equal(["reviewed"], review.Outcomes.Select(static o => o.Value), StringComparer.Ordinal);
        Assert.Equal(["fact:relevanceVerdicts"], Writes(review, "reviewed"), StringComparer.Ordinal);
        Assert.Equal(Capability.Tools, review.Capabilities);
        Assert.Equal(["model"], review.Parameters.Select(static p => p.Name), StringComparer.Ordinal);
    }

    [Fact]
    public void The_judge_reads_the_verdicts_but_is_a_long_block_Step_with_three_parameters()
    {
        var judge = RelevanceSteps.Judge;

        Assert.Equal("step.scope.relevance-judge", judge.Id.Value);
        Assert.Equal(BlockRuntime.Block, judge.Runtime);
        Assert.Equal(["fact:brief", "fact:lenses", "fact:relevanceVerdicts"], Reads(judge), StringComparer.Ordinal);
        Assert.Equal(["ruled"], judge.Outcomes.Select(static o => o.Value), StringComparer.Ordinal);
        Assert.Equal(["fact:relevanceRulings"], Writes(judge, "ruled"), StringComparer.Ordinal);
        Assert.Equal(Capability.None, judge.Capabilities);
        Assert.Equal(TimeSpan.FromMinutes(90), judge.MaxDuration);
        Assert.Equal(["parallel", "threshold", "providers"], judge.Parameters.Select(static p => p.Name), StringComparer.Ordinal);
        Assert.Equal([ParameterType.Number, ParameterType.Number, ParameterType.List], judge.Parameters.Select(static p => p.Type));
    }

    [Fact]
    public void The_apply_Step_reads_five_facts_and_rewrites_the_lenses_and_the_coverage()
    {
        var apply = RelevanceSteps.Apply;

        Assert.Equal("step.scope.relevance-apply", apply.Id.Value);
        Assert.Equal(BlockRuntime.Block, apply.Runtime);
        Assert.Equal(["fact:lenses", "fact:coverage", "fact:brief", "fact:relevanceVerdicts", "fact:relevanceRulings"], Reads(apply), StringComparer.Ordinal);
        Assert.Equal(["applied"], apply.Outcomes.Select(static o => o.Value), StringComparer.Ordinal);
        Assert.Equal(["fact:coverage", "fact:lenses", "fact:mapFixes", "fact:relevance"], Writes(apply, "applied"), StringComparer.Ordinal);
        Assert.Equal(Capability.None, apply.Capabilities);
        Assert.Null(apply.MaxDuration);
    }

    [Fact]
    public void The_propose_Step_now_reads_and_re_reports_the_map_fixes()
    {
        var propose = ScopingSteps.Propose;

        Assert.Contains("fact:mapFixes", Reads(propose), StringComparer.Ordinal);
        Assert.Contains("fact:mapFixes", Writes(propose, "proposed"), StringComparer.Ordinal);
        Assert.Contains("fact:mapFixes", Writes(propose, "skipped"), StringComparer.Ordinal);
    }
}
