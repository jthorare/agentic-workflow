using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Models;
using Aw.Kernel;

namespace Aw.Context.Tests.Core;

public sealed class ModelsSettingsTests
{
    private const string Example = """{"models":{"names":{"codex":{"cheap":"gpt-6-luna","strong":"gpt-5.5"},"cursor":{"cheap":"gpt-5.6-luna-none-fast","strong":"gpt-5"}},"default":{"harnesses":["claude-code","codex","cursor"],"tier":"cheap"},"labeling":{"tier":"cheap","pick":"jev","candidates":3},"conceptNaming":{"tier":"strong"},"decisionExtraction":{"harnesses":["codex","claude-code"]},"adjudication":{"harnesses":["claude-code"],"models":{"claude-code":"opus"}},"mapCleanup":{},"rerank":{"harnesses":["claude-code"]}}}""";

    private static Result<ContextSettings> Read(string json) => SettingsReader.Read(JsonDocument.Parse(json).RootElement.Clone());

    private static ModelRequest Request(ModelPurpose purpose) => new(purpose, "prompt", 100, TimeSpan.FromSeconds(1));

    [Fact, Trait("Lane", "unit")]
    public void Without_the_key_every_default_stands()
    {
        var models = SettingsReader.Read(section: null).Value.Models;

        Assert.Equal(new ModelEntry(), models.Default);
        Assert.Empty(models.Kinds);
        Assert.Equal(ModelNames.None, models.Names);
        Assert.Equal((LabelPick.Off, 3), (models.LabelPick, models.LabelCandidates));
        Assert.Empty(models.Warnings);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_example_of_the_spec_parses()
    {
        var models = Read(Example).Value.Models;

        Assert.Equal(new CliModels("gpt-6-luna", "gpt-5.5"), models.Names.Codex);
        Assert.Equal(new CliModels("gpt-5.6-luna-none-fast", "gpt-5"), models.Names.Cursor);
        Assert.Equal(new CliModels(Cheap: null, Strong: null), models.Names.ClaudeCode);
        Assert.Equal([ModelHarness.ClaudeCode, ModelHarness.Codex, ModelHarness.Cursor], models.Default.Harnesses);
        Assert.Equal(ModelTier.Cheap, models.Default.Tier);
        Assert.Equal((LabelPick.Jev, 3), (models.LabelPick, models.LabelCandidates));
        Assert.Equal(ModelTier.Strong, models.Entry(ModelKind.ConceptNaming).Tier);
        Assert.Equal([ModelHarness.Codex, ModelHarness.ClaudeCode], models.Entry(ModelKind.DecisionExtraction).Harnesses);
        Assert.Equal("opus", models.Entry(ModelKind.Adjudication).Models![ModelHarness.ClaudeCode]);
        Assert.Equal(new ModelEntry(), models.Entry(ModelKind.MapCleanup));
        Assert.Equal([ModelHarness.ClaudeCode], models.Entry(ModelKind.Rerank).Harnesses);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"models":{"adjudication":{"harnesses":["gemini"]}}}""", "context.models.adjudication.harnesses[0]")]
    [InlineData("""{"models":{"adjudication":{"harnesses":["codex","codex"]}}}""", "context.models.adjudication.harnesses[1]")]
    [InlineData("""{"models":{"adjudication":{"harnesses":[]}}}""", "context.models.adjudication.harnesses")]
    [InlineData("""{"models":{"adjudication":{"harnesses":"codex"}}}""", "context.models.adjudication.harnesses")]
    [InlineData("""{"models":{"labeling":{"tier":"medium"}}}""", "context.models.labeling.tier")]
    [InlineData("""{"models":{"default":{"tier":3}}}""", "context.models.default.tier")]
    [InlineData("""{"models":{"labeling":{"candidates":9}}}""", "context.models.labeling.candidates")]
    [InlineData("""{"models":{"labeling":{"candidates":1}}}""", "context.models.labeling.candidates")]
    [InlineData("""{"models":{"labeling":{"candidates":2.5}}}""", "context.models.labeling.candidates")]
    [InlineData("""{"models":{"labeling":{"pick":"yes"}}}""", "context.models.labeling.pick")]
    [InlineData("""{"models":{"rerank":{"pick":"jev"}}}""", "context.models.rerank.pick")]
    [InlineData("""{"models":{"rerank":{"candidates":3}}}""", "context.models.rerank.candidates")]
    [InlineData("""{"models":{"labeling":{"models":{"codex":"-x"}}}}""", "context.models.labeling.models.codex")]
    [InlineData("""{"models":{"labeling":{"models":{"codex":"a..b"}}}}""", "context.models.labeling.models.codex")]
    [InlineData("""{"models":{"labeling":{"models":{"gemini":"x"}}}}""", "context.models.labeling.models.gemini")]
    [InlineData("""{"models":{"labeling":{"models":{"codex":7}}}}""", "context.models.labeling.models.codex")]
    [InlineData("""{"models":{"names":{"codex":{"cheap":""}}}}""", "context.models.names.codex.cheap")]
    [InlineData("""{"models":{"names":{"claude":{"cheap":"haiku"}}}}""", "context.models.names.claude")]
    [InlineData("""{"models":{"names":{"codex":{"medium":"x"}}}}""", "context.models.names.codex.medium")]
    [InlineData("""{"models":{"labeling":{"speed":"fast"}}}""", "context.models.labeling.speed")]
    [InlineData("""{"models":{"summaries":{}}}""", "context.models.summaries")]
    [InlineData("""{"models":{"productSurfaces":{"pick":"jev"}}}""", "context.models.productSurfaces")]
    [InlineData("""{"models":[]}""", "context.models")]
    [InlineData("""{"models":{"labeling":"cheap"}}""", "context.models.labeling")]
    public void A_wrong_value_or_key_is_SND_CONTEXT_001_with_the_key_path_first(string json, string keyPath)
    {
        var error = Read(json).Error;

        Assert.Equal(ContextErrors.SettingsInvalid.Code, error.Code);
        Assert.StartsWith(keyPath + ":", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_messages_say_what_is_allowed_and_suggest_the_harness_that_was_meant()
    {
        Assert.Contains("context.models.adjudication.harnesses[0]: \"claude\" is not one of claude-code, codex, cursor (did you mean claude-code?)", Read("""{"models":{"adjudication":{"harnesses":["claude"]}}}""").Error.Message, StringComparison.Ordinal);
        Assert.Contains("context.models.labeling.tier: \"medium\" is cheap or strong", Read("""{"models":{"labeling":{"tier":"medium"}}}""").Error.Message, StringComparison.Ordinal);
        Assert.Contains("context.models.labeling.models.codex: a model name is letters, digits and . _ : / @ + - , not starting with -", Read("""{"models":{"labeling":{"models":{"codex":"-x"}}}}""").Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_json_null_models_key_is_the_defaults() => Assert.Equal(new ModelsSettings(), Read("""{"models":null}""").Value.Models);

    // The old key and the new one

    [Fact, Trait("Lane", "unit")]
    public void The_old_concept_scan_models_key_alone_supplies_the_names()
    {
        var models = Read("""{"conceptScan":{"models":{"claude":{"cheap":"claude-haiku-5-5"},"codex":{"strong":"codex-big"}}}}""").Value.Models;

        Assert.Equal(new CliModels("claude-haiku-5-5", Strong: null), models.Names.ClaudeCode);
        Assert.Equal(new CliModels(Cheap: null, Strong: "codex-big"), models.Names.Codex);
        Assert.Empty(models.Warnings);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_old_key_profile_resolves_to_the_same_models_as_its_new_key_equivalent()
    {
        var old = Read("""{"conceptScan":{"models":{"claude":{"strong":"opus"},"codex":{"strong":"codex-big"}}}}""").Value.Models;
        var current = Read("""{"models":{"names":{"claude-code":{"strong":"opus"},"codex":{"strong":"codex-big"}}}}""").Value.Models;
        var request = new ModelRequest(ModelPurpose.ConceptScan, "p", 100, TimeSpan.FromSeconds(1), ModelTier.Strong);

        Assert.Equal(ModelRouting.Resolve(current, request, ModelHarnesses.DefaultOrder), ModelRouting.Resolve(old, request, ModelHarnesses.DefaultOrder));
        Assert.Equal("opus", ModelRouting.Resolve(old, request, ModelHarnesses.DefaultOrder)[0].Model);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_new_key_wins_when_both_exist_and_the_profile_check_warns()
    {
        var models = Read("""{"conceptScan":{"models":{"claude":{"cheap":"old-cheap","strong":"old-strong"}}},"models":{"names":{"claude-code":{"cheap":"new-cheap"}}}}""").Value.Models;

        Assert.Equal("new-cheap", models.Names.ClaudeCode.Cheap);
        Assert.Equal("old-strong", models.Names.ClaudeCode.Strong);   // the old key still fills what the new one leaves out
        Assert.Equal(["context.conceptScan.models is replaced by context.models.names; the new key is used"], models.Warnings);
    }

    // The kind map

    [Theory, Trait("Lane", "unit")]
    [InlineData(ModelPurpose.Labeling, ModelKind.Labeling)]
    [InlineData(ModelPurpose.Concepts, ModelKind.ConceptNaming)]
    [InlineData(ModelPurpose.ConceptScan, ModelKind.ConceptNaming)]
    [InlineData(ModelPurpose.ConceptSynthesis, ModelKind.ConceptNaming)]
    [InlineData(ModelPurpose.DecisionExtraction, ModelKind.DecisionExtraction)]
    [InlineData(ModelPurpose.Adjudication, ModelKind.Adjudication)]
    [InlineData(ModelPurpose.Cleanup, ModelKind.MapCleanup)]
    [InlineData(ModelPurpose.Rerank, ModelKind.Rerank)]
    public void Every_purpose_belongs_to_one_kind(ModelPurpose purpose, ModelKind kind) => Assert.Equal(kind, ModelKinds.Of(purpose));

    [Fact, Trait("Lane", "unit")]
    public void Every_purpose_has_a_kind_and_the_scan_purpose_is_written_concept_scan()
    {
        foreach (var purpose in Enum.GetValues<ModelPurpose>())
        {
            Assert.Contains(ModelKinds.Of(purpose), ModelKinds.All);
        }

        Assert.Equal("\"concept-scan\"", JsonSerializer.Serialize(ModelPurpose.ConceptScan));
    }

    [Fact, Trait("Lane", "unit")]
    public void ConceptScan_and_Concepts_use_the_conceptNaming_entry()
    {
        var models = Read("""{"models":{"conceptNaming":{"harnesses":["codex"],"models":{"codex":"gpt-x"}}}}""").Value.Models;

        foreach (var purpose in new[] { ModelPurpose.Concepts, ModelPurpose.ConceptScan, ModelPurpose.ConceptSynthesis })
        {
            Assert.Equal([new RoutedModel(ModelHarness.Codex, "gpt-x", ModelSource.KindModels)], ModelRouting.Resolve(models, Request(purpose), ModelHarnesses.DefaultOrder));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_built_in_names_are_the_pinned_table() =>
        Assert.Equal(new ModelNames(new CliModels("haiku", "sonnet"), new CliModels("gpt-6-luna", "gpt-5.5"), new CliModels("gpt-5.6-luna-none-fast", "gpt-5")), ModelNames.BuiltIn);

    [Fact, Trait("Lane", "unit")]
    public void A_kind_with_only_a_tier_inherits_the_default_harnesses()
    {
        var models = Read("""{"models":{"default":{"harnesses":["cursor","codex"]},"labeling":{"tier":"strong"}}}""").Value.Models;

        var route = ModelRouting.Resolve(models, Request(ModelPurpose.Labeling), ModelHarnesses.DefaultOrder);

        Assert.Equal([new RoutedModel(ModelHarness.Cursor, "gpt-5", ModelSource.Builtin), new RoutedModel(ModelHarness.Codex, "gpt-5.5", ModelSource.Builtin)], route);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_error_codes_240_and_241_are_unavailable_errors_in_the_catalogue()
    {
        Assert.Equal(("SND-CONTEXT-240", ErrorKind.Unavailable), (ContextErrors.ModelHarnessesUnavailable.Code, ContextErrors.ModelHarnessesUnavailable.Kind));
        Assert.Equal(("SND-CONTEXT-241", ErrorKind.Unavailable), (ContextErrors.NoModelHarness.Code, ContextErrors.NoModelHarness.Kind));
        Assert.Contains(ContextErrors.ModelHarnessesUnavailable, ContextErrors.All);
        Assert.Contains(ContextErrors.NoModelHarness, ContextErrors.All);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_model_answer_says_which_harness_answered_and_how_its_tokens_were_counted_and_defaults_to_neither()
    {
        var plain = new ModelAnswer("x", 1, 2);
        var told = plain with { Harness = "codex", TokenBasis = TokenBasis.Reported };

        Assert.Equal((null, TokenBasis.Estimated), (plain.Harness, plain.TokenBasis));
        Assert.Equal(("codex", TokenBasis.Reported), (told.Harness, told.TokenBasis));
    }
}
