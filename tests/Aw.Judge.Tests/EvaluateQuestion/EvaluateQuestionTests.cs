using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Providers;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.EvaluateQuestion;

// 'judge eval <question>': one provider scored against the labels, called directly.
public sealed class EvaluateQuestionTests
{
    private const string RecordPath = "/tmp/rec.jsonl";

    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task EvaluateQuestion_refuses_a_variant_the_question_does_not_have()
    {
        var world = new EvalWorld().Item("i1", "ask");

        var result = await world.Service.Handle(A.EvaluateQuestionRequest().Variant("no-such-variant").Build(), _ct);

        Assert.True(result.IsError);
        Assert.Equal(JudgeErrors.Invalid.Code, result.Error.Code);
        Assert.Equal("unknown variant for ask-check: no-such-variant", result.Error.Message);
        Assert.Empty(world.Jev.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_question_not_in_the_catalog_is_invalid()
    {
        var result = await new EvalWorld().Service.Handle(A.EvaluateQuestionRequest().Question("import-turn").Build(), _ct);

        Assert.Equal(JudgeErrors.Invalid.Code, result.Error.Code);
        Assert.Equal("unknown question: import-turn", result.Error.Message);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_collapse_that_is_not_an_output_is_invalid()
    {
        var world = new EvalWorld().Item("i1", "ask");

        var result = await world.Service.Handle(A.EvaluateQuestionRequest().Collapse("maybe").Build(), _ct);

        Assert.Equal(JudgeErrors.Invalid.Code, result.Error.Code);
        Assert.Contains("--collapse must be one of: continue, ask", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_question_with_no_labelled_items_says_how_to_get_some()
    {
        var result = await new EvalWorld().Service.Handle(A.EvaluateQuestionRequest().Build(), _ct);

        Assert.Equal(JudgeErrors.Invalid.Code, result.Error.Code);
        Assert.Contains("no labeled items for ask-check", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_provider_the_allowlist_excludes_is_refused_before_anything_is_read()
    {
        var world = new EvalWorld().Item("i1", "ask");

        var result = await world.Service.Handle(A.EvaluateQuestionRequest().Allow(ProviderName.ClaudeCli).Build(), _ct);

        Assert.Equal(JudgeErrors.ProviderExcluded.Code, result.Error.Code);
        Assert.Empty(world.Jev.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_provider_that_is_not_installed_is_unavailable()
    {
        var world = new EvalWorld().Item("i1", "ask");

        var result = await world.Service.Handle(A.EvaluateQuestionRequest().Provider(ProviderName.ClaudeCli).Build(), _ct);

        Assert.Equal(JudgeErrors.ProviderUnavailable.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_provider_is_called_directly_per_item_and_scored_against_the_labels()
    {
        var world = new EvalWorld().Item("i1", "ask").Item("i2", "continue").Item("i3", "ask").Item("i4", "continue");
        _ = world.Jev.Decides("ask", 0.9).Decides("continue", 0.55).Decides("continue", 0.8).Returns(new ProviderErrored("exit-1"));

        var result = await world.Service.Handle(A.EvaluateQuestionRequest().Build(), _ct);

        var report = result.Value.Report;
        var sweep = report.Sweep.ToDictionary(s => s.Threshold);
        Assert.Equal(4, world.Jev.Calls.Count);
        Assert.Equal("ask-check", world.Jev.Calls[0].Question.Name);
        Assert.Equal((4, 3), (report.N, report.Decided));
        Assert.Equal(0.5, report.Accuracy);                  // i1 and i2 are right, of four
        Assert.Equal(0.5, report.DecisiveRate);              // 0.9 and 0.8 reach the 0.6 threshold; 0.55 does not
        Assert.Equal(0.5, report.AccuracyDecisive);          // 0.9 is right, 0.8 is wrong
        Assert.Equal([0.5, 0.6, 0.7, 0.8, 0.9], report.Sweep.Select(s => s.Threshold));
        Assert.Equal(0.75, sweep[0.5].Coverage);
        Assert.Equal(0.5, sweep[0.8].Coverage);
        Assert.Equal(0.25, sweep[0.9].Coverage);
        Assert.Equal(1.0, sweep[0.9].AccuracyCovered);
        Assert.Null(report.Labels);
        Assert.Equal("jev", report.Provider);
        Assert.Equal("as-is", report.Variant);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Labels_limits_the_items_to_one_source()
    {
        var world = new EvalWorld()
            .Item("i1", "ask", LabelSource.Outcome)
            .Item("i2", "continue", LabelSource.Adjudicator);
        _ = world.Jev.Decides("ask", 0.9);

        var result = await world.Service.Handle(A.EvaluateQuestionRequest().Labels(LabelSource.Outcome).Build(), _ct);

        Assert.Equal(1, result.Value.Report.N);
        Assert.Equal(LabelSource.Outcome, result.Value.Report.Labels);
        _ = Assert.Single(world.Jev.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Collapse_folds_every_other_label_and_answer_into_stalled()
    {
        var world = new EvalWorld().Item("i1", "ask").Item("i2", "continue");
        _ = world.Jev.Decides("continue", 0.9);

        var result = await world.Service.Handle(A.EvaluateQuestionRequest().Collapse("ask").Build(), _ct);

        // i1: label ask, answer continue folds to stalled: wrong. i2: label continue folds to stalled, as does the answer: right.
        Assert.Equal(0.5, result.Value.Report.Accuracy);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_variant_changes_what_the_provider_is_asked_and_leaves_out_items_without_the_context()
    {
        var world = new EvalWorld();
        _ = world.Store
            .Label("resolution-check", "r1", Resolution(withDiff: true), "resolved")
            .Label("resolution-check", "r2", Resolution(withDiff: false), "partial");
        _ = world.Jev.Decides("resolved", 0.9);

        var stat = await world.Service.Handle(A.EvaluateQuestionRequest().Question("resolution-check").Variant("stat").Build(), _ct);

        Assert.Equal(1, stat.Value.Report.N);   // only the item that stored a diff
        Assert.False(Assert.IsType<JsonObject>(world.Jev.Calls[0].Input).ContainsKey("diff"));
        Assert.DoesNotContain("<diff>", world.Jev.Calls[0].Question.Prompt, StringComparison.Ordinal);

        var asIs = await world.Service.Handle(A.EvaluateQuestionRequest().Question("resolution-check").Build(), _ct);

        Assert.Equal(2, asIs.Value.Report.N);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_input_that_does_not_read_as_the_questions_input_is_an_invalid_input_answer_without_a_call()
    {
        var world = new EvalWorld();
        _ = world.Store.Label(EvalWorld.Question, "i1", """{"unrelated":1}""", "ask");

        var report = (await world.Service.Handle(A.EvaluateQuestionRequest().Build(), _ct)).Value.Report;

        Assert.Empty(world.Jev.Calls);
        Assert.Equal((1, 0), (report.N, report.Decided));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Both_label_sources_give_a_score_per_source_and_a_warning_when_they_disagree()
    {
        var world = new EvalWorld()
            .Item("i1", "ask", LabelSource.Outcome).Item("i1", "continue", LabelSource.Adjudicator)
            .Item("i2", "ask", LabelSource.Outcome).Item("i2", "ask", LabelSource.Adjudicator);
        _ = world.Jev.Decides("ask", 0.9);

        var report = (await world.Service.Handle(A.EvaluateQuestionRequest().Build(), _ct)).Value.Report;

        Assert.NotNull(report.PerSource);
        Assert.Equal(new LabelAgreement(2, 1), report.PerSource.Agreement);
        Assert.Equal(1.0, report.PerSource.Outcome.Accuracy);
        Assert.Equal(0.5, report.PerSource.Adjudicator.Accuracy);
        Assert.Equal(LabelSource.Adjudicator, report.PerSource.Adjudicator.Labels);
        Assert.Contains("disagree on >30%", report.SourceWarning, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Record_then_replay_gives_the_same_report_with_no_provider_call()
    {
        var world = new EvalWorld().Item("i1", "ask").Item("i2", "continue");
        _ = world.Jev.Decides("ask", 0.9).Decides("ask", 0.7);
        var recorded = await world.Service.Handle(A.EvaluateQuestionRequest().Record(RecordPath).Build(), _ct);
        var calls = world.Jev.Calls.Count;

        var replayed = await world.Service.Handle(A.EvaluateQuestionRequest().Replay(RecordPath).Build(), _ct);

        Assert.Equal(2, calls);
        Assert.Equal(calls, world.Jev.Calls.Count);   // the replay asked nobody
        Assert.Equal(recorded.Value.ToJson().ToJsonString(), replayed.Value.ToJson().ToJsonString());
        Assert.Equal(2, Written(world, RecordPath).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_replay_scores_against_the_labels_now_in_the_store_and_the_last_row_per_item_wins()
    {
        var world = new EvalWorld().Item("i1", "ask");
        world.Files.Put(RecordPath, string.Join(
            '\n',
            """{"itemId":"i1","label":"continue","status":"decided","decision":"continue","confidence":0.9,"reasonCode":"model","latencyMs":5}""",
            """{"itemId":"i1","label":"continue","status":"decided","decision":"ask","confidence":0.9,"reasonCode":"model","latencyMs":7}""",
            """{"itemId":"gone","label":"ask","status":"errored","reasonCode":"exit-1","latencyMs":1}""") + "\n");

        var report = (await world.Service.Handle(A.EvaluateQuestionRequest().Replay(RecordPath).Build(), _ct)).Value.Report;

        Assert.Equal(1, report.N);
        Assert.Equal(1.0, report.Accuracy);   // the second row says ask, and the store's label is ask
        Assert.Equal(7, report.P50LatencyMs);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_replay_still_honours_the_allowlist_but_needs_no_installed_provider()
    {
        var world = new EvalWorld().Item("i1", "ask");
        world.Files.Put(RecordPath, """{"itemId":"i1","label":"ask","status":"unavailable","reasonCode":"timeout","latencyMs":1}""" + "\n");

        var excluded = await world.Service.Handle(A.EvaluateQuestionRequest().Replay(RecordPath).Allow(ProviderName.CodexCli).Build(), _ct);
        var notInstalled = await world.Service.Handle(A.EvaluateQuestionRequest().Provider(ProviderName.ClaudeCli).Replay(RecordPath).Build(), _ct);

        Assert.Equal(JudgeErrors.ProviderExcluded.Code, excluded.Error.Code);
        Assert.True(notInstalled.IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_replay_file_that_is_missing_or_not_records_is_invalid()
    {
        var world = new EvalWorld().Item("i1", "ask");
        world.Files.Put("/tmp/bad.jsonl", "not json\n");

        var missing = await world.Service.Handle(A.EvaluateQuestionRequest().Replay("/tmp/none.jsonl").Build(), _ct);
        var bad = await world.Service.Handle(A.EvaluateQuestionRequest().Replay("/tmp/bad.jsonl").Build(), _ct);

        Assert.Equal(JudgeErrors.Invalid.Code, missing.Error.Code);
        Assert.StartsWith("cannot read replay file", missing.Error.Message, StringComparison.Ordinal);
        Assert.Equal("cannot read replay file: line 1 is not an eval record", bad.Error.Message);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_report_is_written_as_json_and_markdown_under_evals()
    {
        var world = new EvalWorld().Item("i1", "ask");
        _ = world.Jev.Decides("ask", 0.9);

        var result = await world.Service.Handle(A.EvaluateQuestionRequest().Build(), _ct);

        var stem = string.Create(CultureInfo.InvariantCulture, $"{InMemoryEvalFiles.ReportsDirectory}/ask-check-jev-as-is-{world.Clock.GetUtcNow().ToUnixTimeMilliseconds()}");
        Assert.Equal(stem + ".md", result.Value.ReportPath);
        Assert.StartsWith("# judge eval: ask-check / jev / as-is", world.Files.Text(stem + ".md"), StringComparison.Ordinal);
        using var json = JsonDocument.Parse(Written(world, stem + ".json"));
        Assert.Equal(1, json.RootElement.GetProperty("n").GetInt32());
        Assert.Equal("any", json.RootElement.GetProperty("labelSource").GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_record_file_never_holds_a_secret()
    {
        var secret = SampleSecrets.AnthropicKey;
        var world = new EvalWorld().Item("i1", "ask");
        _ = world.Jev.Returns(new ProviderErrored("failed " + secret));

        _ = await world.Service.Handle(A.EvaluateQuestionRequest().Record(RecordPath).Build(), _ct);

        var text = Written(world, RecordPath);
        Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
        Assert.Contains(FakeInputRedactor.Placeholder, text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Both_report_files_go_through_the_redactor()
    {
        // A redactor that treats the question's name as a secret: it is in both reports' text, so neither file may keep it.
        var world = new EvalWorld(new FakeInputRedactor("ask-check")).Item("i1", "ask");
        _ = world.Jev.Decides("ask", 0.9);

        var result = await world.Service.Handle(A.EvaluateQuestionRequest().Build(), _ct);

        var markdown = Written(world, result.Value.ReportPath);
        var json = Written(world, result.Value.ReportPath.Replace(".md", ".json", StringComparison.Ordinal));
        Assert.DoesNotContain("ask-check", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("ask-check", json, StringComparison.Ordinal);
        Assert.Contains(FakeInputRedactor.Placeholder, markdown, StringComparison.Ordinal);
        Assert.Contains(FakeInputRedactor.Placeholder, json, StringComparison.Ordinal);
    }

    private static string Written(EvalWorld world, string path) =>
        world.Files.Text(path) ?? throw new InvalidOperationException($"nothing was written to {path}");

    private static string Resolution(bool withDiff)
    {
        var input = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["brief"] = "b",
            ["expected"] = "e",
            ["actual"] = "a",
            ["rootCause"] = "r",
            ["checkKind"] = "test",
            ["checkSummary"] = "c",
            ["beforePassed"] = false,
            ["afterPassed"] = true,
            ["diffStat"] = "1 file",
        };
        if (withDiff)
        {
            input["diff"] = "+x";
        }

        return JsonSerializer.Serialize(input);
    }
}
