using Aw.Judge.Core.Evals;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Providers;
using Aw.Judge.Features.EvaluateQuestion;
using Aw.Judge.Features.ImportTurns;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Tests.EvaluateQuestion;

// 'judge eval …': import-turns, spelled exactly, is the one second word; any other first word is a question. Routing is
// proven with recording services in place of the slices, so these tests do not depend on the stores.
public sealed class EvalCommandTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Theory, Trait("Lane", "integration")]
    [InlineData("a question name is required")]
    [InlineData("unknown option '--bogus'", "--bogus", "x")]
    [InlineData("unknown option '-h'", "-h")]
    [InlineData("--variant needs a value", "ask-check", "--variant")]
    [InlineData("one question at a time", "ask-check", "wake-gate")]
    public async Task An_eval_command_line_it_cannot_read_is_a_usage_error(string problem, params string[] args)
    {
        var run = await new JudgeCli(_state).Run(["eval", .. args]);

        Assert.Equal(new CliRun(64, "", $"usage: judge eval <question> [--provider P] [--variant V] [--labels outcome|adjudicator|override|any] [--collapse LABEL] [--record FILE] [--replay FILE] ({problem})\n"), run);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("a transcript file is required")]
    [InlineData("unknown option '--bogus'", "--bogus", "x")]
    [InlineData("--since needs a value", "t.jsonl", "--since")]
    public async Task An_import_turns_command_line_it_cannot_read_is_a_usage_error(string problem, params string[] args)
    {
        var run = await new JudgeCli(_state).Run(["eval", "import-turns", .. args]);

        Assert.Equal(new CliRun(64, "", $"usage: judge eval import-turns <transcript.jsonl>... [--since 14d] ({problem})\n"), run);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_eval_hands_every_flag_and_the_allowlist_to_the_service_and_prints_the_report()
    {
        var report = Report("claude-cli", LabelSource.Outcome, p50: 12);
        var scorer = new RecordingService<EvaluateQuestionRequest, EvaluateQuestionResponse>(new EvaluateQuestionResponse(report, "/x/r.md"));
        var cli = new JudgeCli(_state) { Overrides = services => services.AddScoped<IService<EvaluateQuestionRequest, EvaluateQuestionResponse>>(_ => scorer) };

        var run = await cli.Run("eval", "ask-check", "--provider", "claude-cli", "--variant", "full", "--labels", "outcome", "--collapse", "ask", "--record", "r.jsonl", "--replay", "p.jsonl", "--providers", "claude-cli,jev");

        var request = Assert.Single(scorer.Requests);
        Assert.Equal(("ask-check", ProviderName.ClaudeCli, "full", (LabelSource?)LabelSource.Outcome, "ask", "r.jsonl", "p.jsonl"), (request.Question, request.Provider, request.Variant, request.Labels, request.Collapse, request.RecordFile, request.ReplayFile));
        Assert.Equal([ProviderName.ClaudeCli, ProviderName.Jev], request.Providers);
        Assert.Equal(0, run.ExitCode);
        Assert.Equal("ask-check", run.Json().GetProperty("question").GetString());
        Assert.Equal("outcome", run.Json().GetProperty("labelSource").GetString());
        Assert.Equal(12, run.Json().GetProperty("p50LatencyMs").GetInt32());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_eval_without_flags_asks_jev_over_any_label_as_is()
    {
        var scorer = new RecordingService<EvaluateQuestionRequest, EvaluateQuestionResponse>(new EvaluateQuestionResponse(Report("jev"), "/x/r.md"));
        var cli = new JudgeCli(_state) { Overrides = services => services.AddScoped<IService<EvaluateQuestionRequest, EvaluateQuestionResponse>>(_ => scorer) };

        var run = await cli.Run("eval", "ask-check", "--labels", "any");

        var request = Assert.Single(scorer.Requests);
        Assert.Equal(("ask-check", ProviderName.Jev, "as-is", null, null, null, null), (request.Question, request.Provider, request.Variant, request.Labels, request.Collapse, request.RecordFile, request.ReplayFile));
        Assert.Null(request.Providers);
        Assert.Equal("any", run.Json().GetProperty("labelSource").GetString());
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("provider not available: nope", "--provider", "nope")]
    [InlineData("--labels must be outcome, adjudicator, override or any", "--labels", "maybe")]
    public async Task An_unknown_provider_or_label_source_is_exit_1_and_asks_nothing(string stderr, params string[] flags)
    {
        var scorer = new RecordingService<EvaluateQuestionRequest, EvaluateQuestionResponse>(new EvaluateQuestionResponse(Report("jev"), "/x"));
        var cli = new JudgeCli(_state) { Overrides = services => services.AddScoped<IService<EvaluateQuestionRequest, EvaluateQuestionResponse>>(_ => scorer) };

        var run = await cli.Run(["eval", "ask-check", .. flags]);

        Assert.Equal(new CliRun(1, "", stderr + "\n"), run);
        Assert.Empty(scorer.Requests);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_provider_the_allowlist_excludes_is_exit_64_from_the_real_service()
    {
        var run = await new JudgeCli(_state).Run("eval", "ask-check", "--providers", "claude-cli");

        Assert.Equal(64, run.ExitCode);
        Assert.Equal("", run.Stdout);
        Assert.Equal("jev is excluded by the providers allowlist\n", run.Stderr);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_mistyped_import_turns_is_a_question_name_not_the_importer()
    {
        var importer = new RecordingService<ImportTurnsRequest, ImportTurnsResponse>(new ImportTurnsResponse(0, 0, 0));
        var cli = new JudgeCli(_state) { Overrides = services => services.AddScoped<IService<ImportTurnsRequest, ImportTurnsResponse>>(_ => importer) };

        var run = await cli.Run("eval", "import-turn", "t.jsonl");

        Assert.Equal(new CliRun(64, "", "usage: judge eval <question> [--provider P] [--variant V] [--labels outcome|adjudicator|override|any] [--collapse LABEL] [--record FILE] [--replay FILE] (one question at a time)\n"), run);
        Assert.Empty(importer.Requests);
        Assert.Equal(
            new CliRun(1, "", "unknown question: import-turn\n"),
            await new JudgeCli(_state).Run("eval", "import-turn"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Import_turns_hands_the_files_and_the_window_to_the_service_and_prints_the_counts()
    {
        var importer = new RecordingService<ImportTurnsRequest, ImportTurnsResponse>(new ImportTurnsResponse(3, 2, 1));
        var cli = new JudgeCli(_state) { Overrides = services => services.AddScoped<IService<ImportTurnsRequest, ImportTurnsResponse>>(_ => importer) };

        var run = await cli.Run("eval", "import-turns", "a.jsonl", "--since", "14d", "b.jsonl");

        var request = Assert.Single(importer.Requests);
        Assert.Equal(["a.jsonl", "b.jsonl"], request.Files);
        Assert.Equal(TimeSpan.FromDays(14), request.Since);
        Assert.Equal(new CliRun(0, """{"imported":3,"skipped":2,"unreadable":1}""" + "\n", ""), run);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Import_turns_without_a_window_imports_every_file()
    {
        var importer = new RecordingService<ImportTurnsRequest, ImportTurnsResponse>(new ImportTurnsResponse(0, 0, 0));
        var cli = new JudgeCli(_state) { Overrides = services => services.AddScoped<IService<ImportTurnsRequest, ImportTurnsResponse>>(_ => importer) };

        _ = await cli.Run("eval", "import-turns", "a.jsonl");

        Assert.Null(Assert.Single(importer.Requests).Since);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("14")]
    [InlineData("d")]
    [InlineData("-3d")]
    [InlineData("1.5d")]
    public async Task A_window_that_does_not_look_like_14d_is_exit_1(string since)
    {
        var run = await new JudgeCli(_state).Run("eval", "import-turns", "a.jsonl", "--since", since);

        Assert.Equal(new CliRun(1, "", "--since must look like 14d\n"), run);
    }

    public void Dispose() => _state.Dispose();

    private static EvalReport Report(string provider, LabelSource? labels = null, long p50 = 0) =>
        new("ask-check", provider, "as-is", labels, N: 0, Decided: 0, Accuracy: 0, DecisiveRate: 0, AccuracyDecisive: 0, P50LatencyMs: p50, Sweep: []);
}
