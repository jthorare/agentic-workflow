using Aw.Judge.Core.Decisions;
using Aw.Judge.Features.LabelOutcomes;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Tests.Labels;

// 'judge label …': each subcommand's stdout and exit, over a real database in a scratch state directory.
public sealed class LabelCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    private async Task Decided(string id, string question, string answer, string inputJson)
    {
        var record = A.Decision().WithId(id).At(KnownInstants.Epoch).Build() with
        {
            Question = question,
            Decision = answer,
            Details = new DecisionDetails { InputJson = inputJson },
        };
        Assert.True((await _state.DecisionLog().Record(record, CancellationToken.None)).IsSuccess);
    }

    private JudgeCli Cli()
    {
        var cli = new JudgeCli(_state);
        cli.Clock.SetUtcNow(KnownInstants.Epoch.AddDays(2));
        return cli;
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Import_prints_the_counts_and_a_second_run_imports_nothing()
    {
        await Decided("d1", "ask-check", "ask", """{"transcriptTail":"a"}""");
        await Decided("d2", "ask-check", "ask", "not json");
        var cli = Cli();

        var first = await cli.Run("label", "import", "--question", "ask-check", "--since", "14d");
        var second = await cli.Run("label", "import");

        Assert.Equal(new CliRun(0, """{"imported":1,"skippedUnparsable":1}""" + "\n", ""), first);
        Assert.Equal(new CliRun(0, """{"imported":0,"skippedUnparsable":1}""" + "\n", ""), second);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Import_reads_since_as_days_with_or_without_the_d()
    {
        await Decided("d1", "ask-check", "ask", """{"transcriptTail":"a"}""");
        var cli = Cli();
        cli.Clock.Advance(TimeSpan.FromDays(10));

        var narrow = await cli.Run("label", "import", "--since", "5");
        var wide = await cli.Run("label", "import", "--since", "30d");

        Assert.Equal("""{"imported":0,"skippedUnparsable":0}""" + "\n", narrow.Stdout);
        Assert.Equal("""{"imported":1,"skippedUnparsable":0}""" + "\n", wide.Stdout);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Set_labels_an_imported_item_and_status_counts_it()
    {
        await Decided("d1", "ask-check", "ask", """{"transcriptTail":"a"}""");
        var cli = Cli();
        _ = await cli.Run("label", "import");

        var set = await cli.Run("label", "set", "item-d1", "continue");
        var status = await cli.Run("label", "status");

        Assert.Equal(new CliRun(0, """{"item":"item-d1","label":"continue"}""" + "\n", ""), set);
        Assert.Equal(0, status.ExitCode);
        Assert.Equal(
            """{"questions":[{"question":"ask-check","items":1,"labeled":1,"skipped":0,"agreement":{"shared":0,"agreed":0}}]}""" + "\n",
            status.Stdout);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Status_of_an_empty_store_lists_no_questions() =>
        Assert.Equal(new CliRun(0, """{"questions":[]}""" + "\n", ""), await Cli().Run("label", "status"));

    [Fact, Trait("Lane", "integration")]
    public async Task Set_of_an_unknown_item_exits_1_naming_it() =>
        Assert.Equal(new CliRun(1, "", "unknown item: nope\n"), await Cli().Run("label", "set", "nope", "skip"));

    [Fact, Trait("Lane", "integration")]
    public async Task Set_of_a_label_the_question_cannot_answer_exits_1_listing_the_choices()
    {
        await Decided("d1", "ask-check", "ask", """{"transcriptTail":"a"}""");
        var cli = Cli();
        _ = await cli.Run("label", "import");

        var run = await cli.Run("label", "set", "item-d1", "wake");

        Assert.Equal(new CliRun(1, "", "label must be one of: continue, ask, skip\n"), run);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Outcomes_prints_the_services_counts()
    {
        var service = new RecordingService<LabelOutcomesRequest, LabelOutcomesResponse>(new LabelOutcomesResponse(Labeled: 3, NoSignal: 1));
        var cli = new JudgeCli(_state)
        {
            Overrides = services => services.AddSingleton<Kernel.Services.IService<LabelOutcomesRequest, LabelOutcomesResponse>>(service),
        };

        var run = await cli.Run("label", "outcomes");

        Assert.Equal(new CliRun(0, """{"labeled":3,"noSignal":1}""" + "\n", ""), run);
        _ = Assert.Single(service.Requests);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("label")]
    [InlineData("label bogus")]
    [InlineData("label status extra")]
    [InlineData("label outcomes extra")]
    [InlineData("label set")]
    [InlineData("label set item-1")]
    [InlineData("label set item-1 a b")]
    [InlineData("label import --question")]
    [InlineData("label import --since")]
    [InlineData("label import --since soon")]
    [InlineData("label import --since 99999999d")]
    [InlineData("label import --bogus x")]
    [InlineData("label import stray")]
    public async Task A_bad_command_line_is_a_usage_error_with_one_usage_line(string commandLine)
    {
        var run = await Cli().Run(commandLine.Split(' '));

        Assert.Equal(64, run.ExitCode);
        Assert.Equal("", run.Stdout);
        Assert.StartsWith("usage: judge label", run.Stderr, StringComparison.Ordinal);
        _ = Assert.Single(run.Stderr.TrimEnd('\n').Split('\n'));
    }

    public void Dispose() => _state.Dispose();
}
