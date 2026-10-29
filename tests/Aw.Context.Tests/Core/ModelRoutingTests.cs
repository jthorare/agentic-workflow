using Aw.Context.Core.Engine;
using Aw.Context.Core.Models;

namespace Aw.Context.Tests.Core;

public sealed class ModelRoutingTests
{
    private static readonly IReadOnlyList<ModelHarness> _all = ModelHarnesses.DefaultOrder;

    private static ModelRequest Request(ModelPurpose purpose = ModelPurpose.Labeling, ModelTier? tier = null, IReadOnlyDictionary<string, string>? models = null, string? model = null) =>
        new(purpose, "prompt", 100, TimeSpan.FromSeconds(1), tier, models, model);

    private static RoutedModel Step(ModelHarness harness, string model, ModelSource source) => new(harness, model, source);

    [Fact, Trait("Lane", "unit")]
    public void With_nothing_set_every_installed_harness_runs_its_built_in_cheap_name_in_the_default_order()
    {
        var route = ModelRouting.Resolve(new ModelsSettings(), Request(), _all);

        Assert.Equal([Step(ModelHarness.ClaudeCode, "haiku", ModelSource.Builtin), Step(ModelHarness.Codex, "gpt-6-luna", ModelSource.Builtin), Step(ModelHarness.Cursor, "gpt-5.6-luna-none-fast", ModelSource.Builtin)], route);
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_installed_harnesses_are_tried_in_the_order_given_and_none_installed_is_an_empty_route()
    {
        Assert.Equal([ModelHarness.Cursor, ModelHarness.Codex], ModelRouting.Resolve(new ModelsSettings(), Request(), [ModelHarness.Cursor, ModelHarness.Codex]).Select(static s => s.Harness));
        Assert.Empty(ModelRouting.Resolve(new ModelsSettings(), Request(), []));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_named_harness_that_is_not_installed_is_skipped_and_the_named_order_is_kept()
    {
        var settings = new ModelsSettings { Default = new ModelEntry(Harnesses: [ModelHarness.Cursor, ModelHarness.ClaudeCode, ModelHarness.Codex]) };

        Assert.Equal([ModelHarness.Cursor, ModelHarness.Codex], ModelRouting.Resolve(settings, Request(), [ModelHarness.Codex, ModelHarness.Cursor]).Select(static s => s.Harness));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_kind_harness_list_beats_the_default_one()
    {
        var settings = new ModelsSettings
        {
            Default = new ModelEntry(Harnesses: [ModelHarness.Cursor]),
            Kinds = new Dictionary<ModelKind, ModelEntry> { [ModelKind.Rerank] = new(Harnesses: [ModelHarness.Codex]) },
        };

        Assert.Equal([ModelHarness.Codex], ModelRouting.Resolve(settings, Request(ModelPurpose.Rerank), _all).Select(static s => s.Harness));
        Assert.Equal([ModelHarness.Cursor], ModelRouting.Resolve(settings, Request(ModelPurpose.Labeling), _all).Select(static s => s.Harness));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_kind_model_beats_its_tier_and_says_where_it_came_from()
    {
        var settings = new ModelsSettings
        {
            Kinds = new Dictionary<ModelKind, ModelEntry> { [ModelKind.Adjudication] = new(Harnesses: [ModelHarness.ClaudeCode], Tier: ModelTier.Strong, Models: new Dictionary<ModelHarness, string> { [ModelHarness.ClaudeCode] = "opus" }) },
        };

        Assert.Equal([Step(ModelHarness.ClaudeCode, "opus", ModelSource.KindModels)], ModelRouting.Resolve(settings, Request(ModelPurpose.Adjudication), _all));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_default_model_beats_names_for_its_harness_only()
    {
        var settings = new ModelsSettings
        {
            Names = new ModelNames(new CliModels("written-haiku", Strong: null), ModelNames.None.Codex, ModelNames.None.Cursor),
            Default = new ModelEntry(Models: new Dictionary<ModelHarness, string> { [ModelHarness.Codex] = "gpt-default" }),
        };

        var route = ModelRouting.Resolve(settings, Request(), _all);

        Assert.Equal([Step(ModelHarness.ClaudeCode, "written-haiku", ModelSource.Names), Step(ModelHarness.Codex, "gpt-default", ModelSource.DefaultModels), Step(ModelHarness.Cursor, "gpt-5.6-luna-none-fast", ModelSource.Builtin)], route);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_kind_model_beats_a_default_model_for_the_same_harness()
    {
        var settings = new ModelsSettings
        {
            Default = new ModelEntry(Models: new Dictionary<ModelHarness, string> { [ModelHarness.Codex] = "gpt-default" }),
            Kinds = new Dictionary<ModelKind, ModelEntry> { [ModelKind.Labeling] = new(Models: new Dictionary<ModelHarness, string> { [ModelHarness.Codex] = "gpt-kind" }) },
        };

        Assert.Equal(Step(ModelHarness.Codex, "gpt-kind", ModelSource.KindModels), ModelRouting.Resolve(settings, Request(), _all)[1]);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_tier_is_the_kind_tier_else_the_default_tier_else_cheap()
    {
        var settings = new ModelsSettings
        {
            Default = new ModelEntry(Tier: ModelTier.Strong),
            Kinds = new Dictionary<ModelKind, ModelEntry> { [ModelKind.Rerank] = new(Tier: ModelTier.Cheap) },
        };

        Assert.Equal("sonnet", ModelRouting.Resolve(settings, Request(ModelPurpose.Labeling), _all)[0].Model);
        Assert.Equal("haiku", ModelRouting.Resolve(settings, Request(ModelPurpose.Rerank), _all)[0].Model);
        Assert.Equal("haiku", ModelRouting.Resolve(new ModelsSettings(), Request(), _all)[0].Model);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_concept_scan_keeps_the_tier_it_picked_and_ignores_the_conceptNaming_tier_but_uses_its_harnesses_and_models()
    {
        var settings = new ModelsSettings
        {
            Kinds = new Dictionary<ModelKind, ModelEntry> { [ModelKind.ConceptNaming] = new(Harnesses: [ModelHarness.Codex, ModelHarness.ClaudeCode], Tier: ModelTier.Cheap, Models: new Dictionary<ModelHarness, string> { [ModelHarness.Codex] = "gpt-named" }) },
        };

        var route = ModelRouting.Resolve(settings, Request(ModelPurpose.ConceptScan, tier: ModelTier.Strong), _all);

        Assert.Equal([Step(ModelHarness.Codex, "gpt-named", ModelSource.KindModels), Step(ModelHarness.ClaudeCode, "sonnet", ModelSource.Builtin)], route);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_naming_calls_that_pick_no_tier_use_the_conceptNaming_tier()
    {
        var settings = new ModelsSettings { Kinds = new Dictionary<ModelKind, ModelEntry> { [ModelKind.ConceptNaming] = new(Tier: ModelTier.Strong) } };

        Assert.Equal("sonnet", ModelRouting.Resolve(settings, Request(ModelPurpose.Concepts), _all)[0].Model);
        Assert.Equal("sonnet", ModelRouting.Resolve(settings, Request(ModelPurpose.ConceptSynthesis), _all)[0].Model);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_request_that_names_a_model_beats_the_profile_for_that_harness_and_the_rest_resolve_as_usual()
    {
        var settings = new ModelsSettings { Default = new ModelEntry(Models: new Dictionary<ModelHarness, string> { [ModelHarness.ClaudeCode] = "profile-model", [ModelHarness.Codex] = "profile-codex" }) };

        var route = ModelRouting.Resolve(settings, Request(models: new Dictionary<string, string>(StringComparer.Ordinal) { ["claude"] = "request-model" }), _all);

        Assert.Equal([Step(ModelHarness.ClaudeCode, "request-model", ModelSource.Request), Step(ModelHarness.Codex, "profile-codex", ModelSource.DefaultModels), Step(ModelHarness.Cursor, "gpt-5.6-luna-none-fast", ModelSource.Builtin)], route);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_request_with_one_model_for_every_harness_names_it_for_each_harness_without_an_entry_of_its_own()
    {
        var route = ModelRouting.Resolve(new ModelsSettings(), Request(models: new Dictionary<string, string>(StringComparer.Ordinal) { ["codex"] = "codex-own" }, model: "any-model"), _all);

        Assert.Equal([Step(ModelHarness.ClaudeCode, "any-model", ModelSource.Request), Step(ModelHarness.Codex, "codex-own", ModelSource.Request), Step(ModelHarness.Cursor, "any-model", ModelSource.Request)], route);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("haiku", true)]
    [InlineData("claude-haiku-5-5", true)]
    [InlineData("gpt-5.5", true)]
    [InlineData("vendor/model:tag@2+x", true)]
    [InlineData("-x", false)]
    [InlineData("a..b", false)]
    [InlineData("", false)]
    [InlineData("has space", false)]
    [InlineData("a;b", false)]
    public void A_model_name_is_checked_as_the_spec_says(string name, bool valid) => Assert.Equal(valid, ModelNames.IsValid(name));
}
