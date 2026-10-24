using Aw.Judge.Core.Batching;
using Aw.Judge.Core.PromptSort;

namespace Aw.Judge.Tests.SortPrompt;

// The sorter's pure side: tier-1 skips, the keyword heuristics and the decisive-band blend.
[Trait("Lane", "unit")]
public sealed class PromptSorterTests
{
    [Theory]
    [InlineData("", "empty")]
    [InlineData("   ", "empty")]
    [InlineData("<local-command-stdout>x</local-command-stdout>", "machine")]
    [InlineData("Caveat: the messages below", "machine")]
    [InlineData("/bugFixOrchestrator FRN-1", "slash-command")]
    [InlineData("/clear", "slash-command")]
    [InlineData("ok", "too-short")]
    [InlineData("sounds good", "confirmation")]
    [InlineData("go ahead", "confirmation")]
    public void Tier1_skips_what_must_not_be_sorted(string prompt, string reason) =>
        Assert.Equal(reason, PromptTier1.SkipReason(prompt));

    [Theory]
    [InlineData("/Users/me/project/file.ts is broken, fix it")]
    [InlineData("add a dark mode toggle to the settings page")]
    [InlineData("yes please also update the readme and the changelog")]
    public void Tier1_sorts_a_real_prompt(string prompt) => Assert.Null(PromptTier1.SkipReason(prompt));

    [Fact]
    public void The_heuristics_read_a_bug_report_and_a_ui_task()
    {
        var outcome = PromptSorter.Heuristic("fix the crash in src/ui/Button.tsx, the button throws on click", "no-jev");

        Assert.Equal("true", outcome.Values.Values["is_bug_report"]);
        Assert.Equal("true", outcome.Values.Values["touches_ui"]);
        Assert.Equal("true", outcome.Values.Values["is_task"]);
        Assert.Equal("true", outcome.Values.Values["scope_defined"]);
        Assert.Equal(("heuristic-only", "no-jev", 0), (outcome.Mode, outcome.Reason, outcome.JudgeDecided));
        Assert.Equal(11, outcome.Axes.Count);
    }

    [Fact]
    public void The_heuristics_call_a_question_trivial_and_not_a_task()
    {
        var outcome = PromptSorter.Heuristic("why does the build take so long?", "no-jev");

        Assert.Equal("false", outcome.Values.Values["is_task"]);
        Assert.Equal("trivial", outcome.Values.Complexity);
        Assert.Equal("clear", outcome.Values.Values["ambiguity"]);
    }

    [Fact]
    public void The_heuristics_call_a_cross_cutting_refactor_large()
    {
        var outcome = PromptSorter.Heuristic("refactor and migrate everything across the codebase, implement the new architecture", "no-jev");

        Assert.Equal("large", outcome.Values.Complexity);
    }

    [Fact]
    public void Jev_counts_only_outside_the_band_and_the_heuristic_keeps_the_rest()
    {
        var answers = new Dictionary<string, BatchAnswer>(StringComparer.Ordinal)
        {
            ["is_task"] = new NoulBatchAnswer(0.9),
            ["touches_ui"] = new NoulBatchAnswer(0.5),
            ["is_bug_report"] = new NoulBatchAnswer(0.1),
        };

        var outcome = PromptSorter.Blend("fix the crash in the page", answers);

        Assert.Equal(("blend", "sorted", 2), (outcome.Mode, outcome.Reason, outcome.JudgeDecided));
        Assert.True(outcome.Judged("is_task"));
        Assert.False(outcome.Judged("touches_ui"));
        Assert.Equal("overrode", outcome.Axes.Single(a => a.Axis == "is_bug_report").Status);
        Assert.Equal("false", outcome.Values.Values["is_bug_report"]);
    }

    [Fact]
    public void A_score_counts_at_its_confidence_and_reads_the_level_with_the_most_probability()
    {
        var answers = new Dictionary<string, BatchAnswer>(StringComparer.Ordinal)
        {
            ["complexity"] = new ScoreBatchAnswer(2.4, 0.8, new Dictionary<string, double>(StringComparer.Ordinal) { ["large: a cross-cutting refactor, a new subsystem, or multi-day work"] = 0.7, ["substantial"] = 0.3 }),
            ["ambiguity"] = new ScoreBatchAnswer(2, 0.2, new Dictionary<string, double>(StringComparer.Ordinal) { ["2"] = 1 }),
        };

        var outcome = PromptSorter.Blend("add a thing", answers);

        Assert.Equal("large", outcome.Values.Complexity);
        Assert.True(outcome.Judged("complexity"));
        Assert.False(outcome.Judged("ambiguity"));
    }

    [Fact]
    public void An_ambiguity_score_weighs_partly_below_the_vague_line()
    {
        var answers = new Dictionary<string, BatchAnswer>(StringComparer.Ordinal)
        {
            ["ambiguity"] = new ScoreBatchAnswer(1, 0.9, new Dictionary<string, double>(StringComparer.Ordinal) { ["1"] = 0.8, ["2"] = 0.2 }),
        };

        var outcome = PromptSorter.Blend("add a thing", answers);

        Assert.Equal("partly", outcome.Values.Values["ambiguity"]);
    }

    [Fact]
    public void Answers_that_are_all_inside_the_band_leave_the_heuristics_in_charge()
    {
        var answers = new Dictionary<string, BatchAnswer>(StringComparer.Ordinal) { ["is_task"] = new NoulBatchAnswer(0.5) };

        var outcome = PromptSorter.Blend("add a thing", answers);

        Assert.Equal(("heuristic-only", "all-undecided", 0), (outcome.Mode, outcome.Reason, outcome.JudgeDecided));
    }

    [Fact]
    public void A_prompt_that_names_a_workflow_is_never_scaffolded()
    {
        Assert.True(ScaffoldPlanner.NamesWorkflow("run /bugFixOrchestrator on FRN-12"));
        Assert.False(ScaffoldPlanner.NamesWorkflow("fix the bug"));
    }
}
