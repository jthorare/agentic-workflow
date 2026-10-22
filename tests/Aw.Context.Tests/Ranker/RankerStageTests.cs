using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Models;
using Aw.Context.Core.Ranking;
using Aw.Context.Core.Results;
using Aw.Context.Features.QueryContext;
using Aw.Context.Testing.Engine;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Ranker;

public sealed class RankerStageTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly RecordSignals _best = new(CascadeDistance: 0, EdgeType.Decides, Provenance.Human, Superseded: false, _now);
    private static readonly IReadOnlyDictionary<string, RecordSignals> _none = new Dictionary<string, RecordSignals>(StringComparer.Ordinal);
    private readonly FakeModelPort _model = new();
    private readonly FakeScrubberFactory _scrubbers = new();

    private RankerStage Stage() => new(_model, new FakeTimeProvider(_now));

    private ValueTask<RankedItems> Run(RankerPlan plan, IReadOnlyList<FusedItem> items, IReadOnlyDictionary<string, RecordSignals>? signals = null) =>
        Stage().Apply(items, plan, signals ?? _none, _scrubbers.For([]).Value.Egress, TestContext.Current.CancellationToken);

    private static RankerPlan Plan(bool features = false, bool rerank = false, int top = 30) => new(features, new FeatureWeights(), rerank, top);

    private static string[] Keys(RankedItems ranked) => [.. ranked.Items.Select(i => i.Key.Value)];

    [Fact, Trait("Lane", "unit")]
    public void The_plan_is_the_profiles_unless_a_backtest_arm_names_a_mode()
    {
        var on = new RankerSettings { Features = true, Rerank = true, RerankTop = 12, Weights = new FeatureWeights { Recency = 3 } };

        Assert.Equal((false, false), Pair(RankerPlan.For(new RankerSettings(), mode: null)));
        Assert.Equal((true, true), Pair(RankerPlan.For(on, mode: null)));
        Assert.Equal((false, false), Pair(RankerPlan.For(on, RankerMode.Rrf)));
        Assert.Equal((true, false), Pair(RankerPlan.For(new RankerSettings(), RankerMode.Features)));
        Assert.Equal((true, true), Pair(RankerPlan.For(new RankerSettings(), RankerMode.Rerank)));
        Assert.Equal((12, 3d), (RankerPlan.For(on, RankerMode.Features).Top, RankerPlan.For(on, RankerMode.Features).Weights.Recency));
    }

    private static (bool, bool) Pair(RankerPlan plan) => (plan.Features, plan.Rerank);

    [Fact, Trait("Lane", "unit")]
    public async Task With_both_stages_off_nothing_changes_and_no_model_is_called()
    {
        var ranked = await Run(Plan(), FeatureRankerTests.Fused("a", "b"), new Dictionary<string, RecordSignals>(StringComparer.Ordinal) { ["repo:b"] = _best });

        Assert.Equal(["repo:a", "repo:b"], Keys(ranked));
        Assert.Null(ranked.Degraded);
        Assert.Empty(_model.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_feature_stage_re_orders_by_the_signals_without_calling_a_model()
    {
        var ranked = await Run(Plan(features: true), FeatureRankerTests.Fused("a", "b"), new Dictionary<string, RecordSignals>(StringComparer.Ordinal) { ["repo:b"] = _best });

        Assert.Equal(["repo:b", "repo:a"], Keys(ranked));
        Assert.Empty(_model.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_rerank_asks_the_model_for_the_top_titles_and_takes_its_order()
    {
        _model.Answer = "[2,1,3]";

        var ranked = await Run(Plan(rerank: true, top: 2), FeatureRankerTests.Fused("a", "b", "c"));

        var request = Assert.Single(_model.Requests);
        Assert.Equal(ModelPurpose.Rerank, request.Purpose);
        Assert.Contains("Title a", request.Prompt, StringComparison.Ordinal);
        Assert.Contains("Title b", request.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Title c", request.Prompt, StringComparison.Ordinal);   // only the top 2 are listed
        Assert.True(request.MaxTokens > 0 && request.Budget > TimeSpan.Zero);
        Assert.Equal(["repo:b", "repo:a", "repo:c"], Keys(ranked));
        Assert.Null(ranked.Degraded);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_rerank_orders_what_the_features_left()
    {
        _model.Answer = "[1,2]";

        var ranked = await Run(Plan(features: true, rerank: true, top: 2), FeatureRankerTests.Fused("a", "b"), new Dictionary<string, RecordSignals>(StringComparer.Ordinal) { ["repo:b"] = _best });

        Assert.Contains("1: ", _model.Requests.Single().Prompt, StringComparison.Ordinal);
        Assert.True(_model.Requests.Single().Prompt.IndexOf("Title b", StringComparison.Ordinal) < _model.Requests.Single().Prompt.IndexOf("Title a", StringComparison.Ordinal));   // listed in feature order
        Assert.Equal(["repo:b", "repo:a"], Keys(ranked));   // the model kept it
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_model_that_fails_leaves_the_order_and_is_told_as_degraded_090()
    {
        _model.Fail = _ => ContextErrors.SourceUnavailable.With("no agent CLI");

        var ranked = await Run(Plan(rerank: true), FeatureRankerTests.Fused("a", "b"));

        Assert.Equal(["repo:a", "repo:b"], Keys(ranked));
        Assert.Equal(("rerank", "SND-CONTEXT-090"), (ranked.Degraded!.Source.Value, ranked.Degraded.Code));
        Assert.Equal(_now, ranked.Degraded.Since);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_answer_that_is_no_order_leaves_the_order_and_is_degraded_090()
    {
        _model.Answer = "I cannot rank these.";

        var ranked = await Run(Plan(rerank: true), FeatureRankerTests.Fused("a", "b"));

        Assert.Equal(["repo:a", "repo:b"], Keys(ranked));
        Assert.Equal("SND-CONTEXT-090", ranked.Degraded!.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task One_item_has_nothing_to_order_so_no_model_is_called()
    {
        var ranked = await Run(Plan(rerank: true), FeatureRankerTests.Fused("a"));

        Assert.Equal(["repo:a"], Keys(ranked));
        Assert.Empty(_model.Requests);
        Assert.Null(ranked.Degraded);
    }
}
