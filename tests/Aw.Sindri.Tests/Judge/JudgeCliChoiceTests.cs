using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Infrastructure.Judge;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Tests.Judge;

/// <summary>The judge adapter: the command line and input it sends, the envelopes it reads and the failures it names.</summary>
[Lane(TestLane.Unit)]
public sealed class JudgeCliChoiceTests
{
    private sealed class Runner(ToolRun run) : IToolRunner
    {
        public string? FileName { get; private set; }

        public IReadOnlyList<string> Arguments { get; private set; } = [];

        public ToolRunOptions? Options { get; private set; }

        public ToolRun Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, ToolRunOptions? options = null, CancellationToken cancellationToken = default)
        {
            FileName = fileName;
            Arguments = arguments;
            Options = options;
            return run;
        }
    }

    private static readonly ChooseRequest _request = new(
        "Which evidence?",
        [new ChooseOption("ui", "a user sees it"), new ChooseOption("api", "a contract changed")],
        new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:built"] = "built it" },
        0.7,
        ["claude-cli", "rules"]);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (JudgeCliChoice Port, Runner Runner) Port(ToolRun run, string? program = "/bin/judge")
    {
        var runner = new Runner(run);
        return (new JudgeCliChoice(runner, name => string.Equals(name, "AW_SINDRI_JUDGE", StringComparison.Ordinal) ? program : null, "/home/sindri/judge"), runner);
    }

    [Fact]
    public async Task It_runs_choose_with_the_providers_the_question_on_stdin_and_the_judge_store_in_sindris_home()
    {
        var (port, runner) = Port(new ToolRun(ToolOutcome.Exited, 2, """{"escalated":true,"reasonCode":"below-threshold","id":"d-1"}"""));

        _ = await port.Choose(_request, Token);

        Assert.Equal("/bin/judge", runner.FileName);
        Assert.Equal(["choose", "--providers", "claude-cli,rules"], runner.Arguments, StringComparer.Ordinal);
        Assert.Equal("/home/sindri/judge", runner.Options!.Environment!["AW_JUDGE_STATE_DIR"]);
        var input = JsonNode.Parse(runner.Options.StandardInput!)!;
        Assert.Equal("Which evidence?", (string?)input["prompt"]);
        Assert.Equal("built it", (string?)input["state"]!["fact:built"]);
        Assert.Equal("api", (string?)input["choices"]![1]!["name"]);
        Assert.Equal("a contract changed", (string?)input["choices"]![1]!["criterion"]);
        Assert.Equal(0.7, (double?)input["threshold"]);
    }

    [Fact]
    public async Task Without_AW_SINDRI_JUDGE_it_runs_judge_from_the_path()
    {
        var (port, runner) = Port(new ToolRun(ToolOutcome.Exited, 2, """{"escalated":true,"reasonCode":"below-threshold"}"""), program: null);

        _ = await port.Choose(_request, Token);

        Assert.Equal("judge", runner.FileName);
    }

    [Fact]
    public async Task A_decided_envelope_is_the_choice_with_its_confidence_provider_and_probabilities()
    {
        var (port, _) = Port(new ToolRun(ToolOutcome.Exited, 0, """{"decision":"api","confidence":0.83,"model":"claude-cli","reasonCode":"model","id":"d-9","probabilities":{"ui":0.1,"api":0.83}}"""));

        var answer = await port.Choose(_request, Token);

        Assert.Equal("api", answer.Choice);
        Assert.Equal(ChooseStatus.Decided, answer.Status);
        Assert.Equal("model", answer.Reason);
        Assert.Equal(0.83, answer.Confidence);
        Assert.Equal("claude-cli", answer.Provider);
        Assert.Equal("d-9", answer.DecisionRef);
        Assert.Equal(0.83, answer.Probabilities!["api"]);
    }

    [Theory]
    [InlineData("below-threshold")]
    [InlineData("no-provider-decided")]
    [InlineData("rule-timeout")]
    public async Task These_escalations_are_escalated_with_the_probabilities_the_providers_sent(string reason)
    {
        var (port, _) = Port(new ToolRun(ToolOutcome.Exited, 2, $$$"""{"escalated":true,"reasonCode":"{{{reason}}}","id":"d-2","probabilities":{"ui":0.4}}"""));

        var answer = await port.Choose(_request, Token);

        Assert.Equal(ChooseStatus.Escalated, answer.Status);
        Assert.Equal(reason, answer.Reason);
        Assert.Equal("d-2", answer.DecisionRef);
        Assert.Equal(0.4, answer.Probabilities!["ui"]);
    }

    [Theory]
    [InlineData("invalid-input")]
    [InlineData("question-disabled")]
    public async Task The_judge_refusing_the_question_is_a_failure(string reason)
    {
        var (port, _) = Port(new ToolRun(ToolOutcome.Exited, 2, $$"""{"escalated":true,"reasonCode":"{{reason}}","id":"d-3"}"""));

        var answer = await port.Choose(_request, Token);

        Assert.Equal(ChooseStatus.Failed, answer.Status);
        Assert.Equal(reason, answer.Reason);
    }

    [Theory]
    [InlineData("Missing")]
    [InlineData("TimedOut")]
    public async Task A_missing_or_timed_out_judge_is_a_failure_naming_the_fix(string outcomeName)
    {
        var outcome = Enum.Parse<ToolOutcome>(outcomeName);
        var (port, _) = Port(new ToolRun(outcome, -1, ""));

        var answer = await port.Choose(_request, Token);

        Assert.Equal(ChooseStatus.Failed, answer.Status);
        Assert.Contains(outcome.ToString(), answer.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(64)]
    public async Task Another_exit_code_is_a_failure_with_the_tail_of_stderr(int exit)
    {
        var (port, _) = Port(new ToolRun(ToolOutcome.Exited, exit, "", "judge: boom\n"));

        var answer = await port.Choose(_request, Token);

        Assert.Equal(ChooseStatus.Failed, answer.Status);
        Assert.Contains("judge: boom", answer.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "not json")]
    [InlineData(0, """{"decision":"api"}""")]
    [InlineData(2, "not json")]
    [InlineData(2, """{"decision":"api","confidence":1,"model":"rules"}""")]
    public async Task An_unreadable_envelope_is_a_failure(int exit, string output)
    {
        var (port, _) = Port(new ToolRun(ToolOutcome.Exited, exit, output));

        var answer = await port.Choose(_request, Token);

        Assert.Equal(ChooseStatus.Failed, answer.Status);
    }
}
