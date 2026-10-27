using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Tests.Adjudicate;

// 'judge adjudicate <question> [--limit 60]' on the command line: the summary is JSON on stdout; a command line the judge
// can't read, an unknown question and a --providers list without claude-cli are usage errors (64); no claude on PATH is 1.
public sealed class AdjudicateCliTests : IDisposable
{
    private const string Usage = "usage: judge adjudicate <question> [--limit <n>]";

    private readonly TempStateDirectory _state = new();

    [Theory, Trait("Lane", "integration")]
    [InlineData("adjudicate")]
    [InlineData("adjudicate ask-check extra")]
    [InlineData("adjudicate ask-check --limit")]
    [InlineData("adjudicate ask-check --limit 0")]
    [InlineData("adjudicate ask-check --limit -3")]
    [InlineData("adjudicate ask-check --limit many")]
    [InlineData("adjudicate ask-check --limit 5 --limit 6")]
    [InlineData("adjudicate --limit 5")]
    [InlineData("adjudicate ask-check --verbose")]
    public async Task A_command_line_it_cant_read_is_a_usage_error(string commandLine)
    {
        var run = await new JudgeCli(_state) { Providers = [Claude()] }.Run(commandLine.Split(' '));

        Assert.Equal(new CliRun(64, "", Usage + "\n"), run);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_question_the_judge_does_not_ask_is_a_usage_error()
    {
        var run = await new JudgeCli(_state) { Providers = [Claude()] }.Run("adjudicate", "no-such-question");

        Assert.Equal(64, run.ExitCode);
        Assert.Equal("", run.Stdout);
        Assert.Contains("unknown question: no-such-question", run.Stderr, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("--providers", "jev", "adjudicate", "ask-check")]
    [InlineData("adjudicate", "ask-check", "--providers", "codex-cli,jev")]
    public async Task A_providers_list_without_claude_cli_exits_64(params string[] args)
    {
        var run = await new JudgeCli(_state) { Providers = [Claude()] }.Run(args);

        Assert.Equal(64, run.ExitCode);
        Assert.Equal("", run.Stdout);
        Assert.Contains("claude-cli is excluded by the providers allowlist", run.Stderr, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_providers_list_that_cant_be_read_exits_64()
    {
        var run = await new JudgeCli(_state) { Providers = [Claude()] }.Run("adjudicate", "ask-check", "--providers", "bogus");

        Assert.Equal(64, run.ExitCode);
        Assert.Equal("", run.Stdout);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Without_the_claude_cli_it_exits_1()
    {
        var run = await new JudgeCli(_state).Run("adjudicate", "ask-check");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal("", run.Stdout);
        Assert.Contains("claude-cli is not available", run.Stderr, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_summary_is_json_on_stdout_and_the_label_is_stored()
    {
        var store = new AdjudicateEvalStore().With(new EvalItem(EvalItemId.From("item-a"), "ask-check", """{"transcriptTail":"t"}""", "decision:a", ModelDecision: null, DateTimeOffset.UnixEpoch));
        var cli = new JudgeCli(_state)
        {
            Providers = [Claude().Decides("ask", 1)],
            Overrides = services => services.AddScoped<IEvalStore>(_ => store),
        };

        var run = await cli.Run("adjudicate", "ask-check", "--limit", "5");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("", run.Stderr);
        var summary = run.Json();
        Assert.Equal(1, summary.GetProperty("labeled").GetInt32());
        Assert.Equal(0, summary.GetProperty("split").GetInt32());
        Assert.Equal(0, summary.GetProperty("failed").GetInt32());
        Assert.Equal("ask", store.Labels[(EvalItemId.From("item-a"), LabelSource.Adjudicator)].Label);
    }

    public void Dispose() => _state.Dispose();

    private static ScriptedProvider Claude() => new(ProviderName.ClaudeCli, [.. ContentClasses.All]);
}
