using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;

namespace Aw.Sindri.Tests.Scoping.GapSearch;

/// <summary>The gap search's descriptors (issue 966, spec 4.4 to 4.6): what each runs on, reads and writes. The searcher's blindness is its read list, so a test pins it.</summary>
[Lane(TestLane.Unit)]
public sealed class GapSearchDescriptorTests
{
    private static readonly string[] _hidden = ["fact:lenses", "fact:coverage", "fact:scopeMap", "fact:anchors", "fact:relevance", "fact:challenge", "fact:findings"];

    private static string[] Reads(BlockDescriptor step) => [.. step.Inputs.Select(static f => f.Value)];

    private static string[] Writes(BlockDescriptor step) => [.. step.Outputs.Select(static o => o.Fact.Value + "=" + string.Join(',', o.RequiredOn.Select(static r => r.Value)))];

    [Fact]
    public void The_searcher_reads_the_brief_the_acceptance_list_and_the_answers_and_nothing_the_gather_found()
    {
        var reads = Reads(GapSearchSteps.GapSearch);

        Assert.Equal(["fact:brief", "fact:acceptance", "fact:answerLog"], reads, StringComparer.Ordinal);
        Assert.Empty(reads.Intersect(_hidden, StringComparer.Ordinal));
    }

    [Fact]
    public void The_searcher_is_an_agent_with_tools_that_takes_a_model_and_reports_its_candidates()
    {
        var step = GapSearchSteps.GapSearch;

        Assert.Equal(BlockRuntime.Agent, step.Runtime);
        Assert.Equal(Capability.Tools, step.Capabilities);
        Assert.Equal(["searched"], step.Outcomes.Select(static o => o.Value), StringComparer.Ordinal);
        Assert.Equal(["fact:gapCandidates=searched"], Writes(step), StringComparer.Ordinal);
        var model = Assert.Single(step.Parameters);
        Assert.Equal(("model", ParameterType.Text, false), (model.Name, model.Type, model.Required));
    }

    [Fact]
    public void The_judge_is_a_long_block_Step_that_reads_what_it_projects_and_rules()
    {
        var step = GapSearchSteps.GapJudge;

        Assert.Equal(BlockRuntime.Block, step.Runtime);
        Assert.Equal(Capability.None, step.Capabilities);
        Assert.Equal(TimeSpan.FromMinutes(90), step.MaxDuration);
        Assert.Equal(["fact:brief", "fact:acceptance", "fact:scopeMap", "fact:lenses", "fact:gapCandidates"], Reads(step), StringComparer.Ordinal);
        Assert.Equal(["ruled"], step.Outcomes.Select(static o => o.Value), StringComparer.Ordinal);
        Assert.Equal(["fact:gapRulings=ruled"], Writes(step), StringComparer.Ordinal);
        Assert.Equal(["parallel", "threshold", "providers"], step.Parameters.Select(static p => p.Name), StringComparer.Ordinal);
    }

    [Fact]
    public void The_log_is_a_block_Step_with_two_outcomes_and_a_latch_only_on_clear()
    {
        var step = GapSearchSteps.GapLog;

        Assert.Equal(BlockRuntime.Block, step.Runtime);
        Assert.Equal(["fact:gapSearch", "fact:gapCandidates", "fact:gapRulings", "fact:findings"], Reads(step), StringComparer.Ordinal);
        Assert.Equal(["clear", "confirmed"], step.Outcomes.Select(static o => o.Value), StringComparer.Ordinal);
        Assert.Equal(["fact:gapSearch=clear,confirmed", "fact:gapAnchors=clear,confirmed", "fact:findings=confirmed", "fact:gapSearchClosed=clear"], Writes(step), StringComparer.Ordinal);
    }

    [Fact]
    public void Reanchor_also_reads_the_gap_anchors() =>
        Assert.Contains("fact:gapAnchors", Reads(ScopingSteps.Reanchor), StringComparer.Ordinal);

    [Theory]
    [InlineData("draft")]
    [InlineData("challenge")]
    public void The_drafter_and_the_challenger_take_a_model(string which)
    {
        var step = which == "draft" ? ScopingSteps.Draft : ScopingSteps.Challenge;

        var model = Assert.Single(step.Parameters);
        Assert.Equal(("model", ParameterType.Text, false), (model.Name, model.Type, model.Required));
    }

    [Fact]
    public void The_recipes_gap_search_and_relevance_states_have_no_inputs()
    {
        var lines = File.ReadAllLines(Path.Combine(RepoRoot(), "recipes", "sindri", "workflow.scoping.yaml"));

        foreach (var state in new[] { "gap-search", "relevance" })
        {
            var start = Array.FindIndex(lines, l => l.StartsWith("  " + state + ":", StringComparison.Ordinal));
            Assert.True(start >= 0, $"the recipe has no state '{state}'");
            var body = lines.Skip(start).TakeWhile((l, i) => i == 0 || l.Length == 0 || l.StartsWith("   ", StringComparison.Ordinal)).ToArray();
            Assert.DoesNotContain(body, static l => l.Contains("inputs:", StringComparison.Ordinal));
        }
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("AgenticWorkflow.slnx not found above the test assembly");
    }
}
