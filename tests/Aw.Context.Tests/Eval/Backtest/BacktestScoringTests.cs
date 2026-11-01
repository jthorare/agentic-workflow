using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Eval.Backtest;

public sealed class BacktestScoringTests
{
    private static GraphSnapshot AsOf() => new GraphBuilder()
        .Edge("code:src/a.ts", EdgeType.Implements, "feature:a.one")
        .Edge("code:src/b.ts", EdgeType.Implements, "feature:b.two")
        .Edge("code:src/b.ts", EdgeType.Implements, "feature:b.three")
        .Build();

    private static ContextResult Answer(string[] nodes, string[] files) =>
        Answers.Of([.. nodes.Select(n => ("graph", Answers.Item(n))), .. files.Select(f => ("code", Answers.Item("code:" + f)))]);

    [Fact, Trait("Lane", "unit")]
    public void Truth_nodes_come_only_from_the_samples_as_of_map()
    {
        // feature:tabs.later exists in no as-of map, so it can never be truth even though src/c.ts changed in the merge
        var truth = BacktestScoring.TruthNodes(AsOf(), ["src/a.ts", "src/c.ts"]);

        Assert.Equal(["feature:a.one"], truth);
    }

    [Fact, Trait("Lane", "unit")]
    public void Sample_without_truth_node_is_unmapped_and_kept_for_file_recall()
    {
        var sample = Builders.Sample("SPL-101", "src/c.ts");

        var score = BacktestScoring.Score(sample, "graph", truthNodes: [], Answer(["feature:a.one"], ["src/c.ts"]), armLeaky: false);

        Assert.True(score.Unmapped);
        Assert.Null(score.NodeRecall);
        Assert.Null(score.NodePrecision);
        Assert.Equal(1.0, score.FileRecallAt20);
    }

    [Fact, Trait("Lane", "unit")]
    public void File_recall_at_20_counts_truth_files_in_the_top_20()
    {
        var files = Enumerable.Range(0, 25).Select(i => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"src/f{i}.ts")).ToList();
        var sample = Builders.Sample("SPL-101", "src/f3.ts", "src/f19.ts", "src/f20.ts", "src/f24.ts");

        var score = BacktestScoring.Score(sample, "graph", ["feature:a.one"], Answer([], [.. files]), armLeaky: false);

        Assert.Equal(0.5, score.FileRecallAt20, 6);   // f3 and f19 are in the first 20; f20 and f24 are not
        Assert.Equal(20, score.PredictedFiles.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void Node_precision_is_strict_without_adjudication()
    {
        var sample = Builders.Sample("SPL-101", "src/b.ts");
        var truth = BacktestScoring.TruthNodes(AsOf(), sample.TruthFiles);

        var score = BacktestScoring.Score(sample, "graph", truth, Answer(["feature:b.two", "feature:a.one", "feature:z.near-miss"], []), armLeaky: false);

        Assert.Equal(0.5, score.NodeRecall);   // b.two found, b.three missed
        Assert.Equal(0.3333, score.NodePrecision);   // one of three named nodes is truth; the other two are misses
    }

    [Fact, Trait("Lane", "unit")]
    public void Unmeasured_metric_is_not_measured_never_zero()
    {
        var unmapped = BacktestScoring.Score(Builders.Sample("SPL-101", "src/c.ts"), "graph", [], Answer([], []), armLeaky: false);

        var arm = BacktestScoring.Aggregate([unmapped], tokens: 0);

        Assert.Null(arm.NodeRecall);
        Assert.Null(arm.NodePrecision);
        Assert.Equal(0.0, arm.FileRecallAt20);
        Assert.Equal((1, 0), (arm.Samples, arm.Mapped));
        Assert.Null(BacktestScoring.Aggregate([], 0).FileRecallAt20);
    }

    [Fact, Trait("Lane", "unit")]
    public void Leaky_share_counts_samples_with_any_leaky_input()
    {
        var clean = Builders.Sample("SPL-101") with { Leaky = false };
        var edited = Builders.Sample("SPL-102") with { Leaky = true };
        var answer = Answer([], []);

        var floor = BacktestScoring.Aggregate([BacktestScoring.Score(clean, "graph", [], answer, armLeaky: false), BacktestScoring.Score(edited, "graph", [], answer, armLeaky: false)], 0);
        var ceiling = BacktestScoring.Aggregate([BacktestScoring.Score(clean, "graph+reviewed", [], answer, armLeaky: true)], 0);

        Assert.Equal(0.5, floor.LeakyShare);
        Assert.Equal(1.0, ceiling.LeakyShare);   // the ceiling arm reads the current map: leaky whatever the brief
    }

    [Fact, Trait("Lane", "unit")]
    public void Predicted_nodes_are_the_graph_and_affects_sections_plus_the_anchors_without_file_nodes()
    {
        var result = Answers.Of(("graph", Answers.Item("feature:a.one")), ("affects", Answers.Item("feature:b.two")), ("code", Answers.Item("code:src/a.ts")), ("records", Answers.Item(node: null))) with
        {
            Anchors = ["feature:a.one", "action:x.y"],
        };

        Assert.Equal(["action:x.y", "feature:a.one", "feature:b.two"], BacktestScoring.PredictedNodes(result));
    }

    [Fact, Trait("Lane", "unit")]
    public void Score_ranks_the_first_truth_file_and_counts_truth_files_in_the_first_ten()
    {
        var sample = Builders.Sample("SPL-101", "src/a.ts", "src/b.ts");
        string[] files = ["src/x.ts", "src/a.ts", .. Enumerable.Range(0, 9).Select(i => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"src/f{i}.ts")), "src/b.ts"];   // a at rank 2, b at rank 12

        var score = BacktestScoring.Score(sample, "ranker-pin", [], Answer([], files), armLeaky: false);

        Assert.Equal((0.5, 0.5), (score.FileRecallAt10, score.ReciprocalRank));
    }
}
