using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Providers;
using Aw.Judge.Infrastructure.Sqlite;
using Aw.Kernel.Data;
using static Aw.Judge.Tests.AskQuestion.AskQuestionHarness;

namespace Aw.Judge.Tests.AskQuestion;

// The contract of 'judge <question> < input.json': one envelope for every question (docs/csharp/judge.md), and the hook
// contract's exception for ask-check's exit codes.
public sealed class AskQuestionCliTests : IDisposable
{
    private const string Ack = """{"text":"ok","senderKind":"teammate"}""";
    private const string Progress = """{"text":"progress on the parser, half done","senderKind":"teammate"}""";

    private readonly TempStateDirectory _state = new();

    private static string Names(JsonElement json) => string.Join(',', json.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));

    [Fact, Trait("Lane", "integration")]
    public async Task A_decided_question_prints_the_decided_envelope_and_exits_0()
    {
        var cli = new JudgeCli(_state);

        var run = await cli.RunWithInput(Ack, "wake-gate");

        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        var json = run.Json();
        Assert.Equal("confidence,decision,id,model,reasonCode", Names(json));
        Assert.Equal(("drop", 1.0, "rules", "pre-rule"), (json.GetProperty("decision").GetString(), json.GetProperty("confidence").GetDouble(), json.GetProperty("model").GetString(), json.GetProperty("reasonCode").GetString()));
        CliGolden.Match(run, "AskQuestion/Golden/decided.json");
        Assert.Equal(0, (await cli.Run("why", json.GetProperty("id").GetString()!)).ExitCode);   // why: the envelope's id is a string
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_question_nothing_decides_prints_the_escalated_envelope_and_exits_2()
    {
        var run = await new JudgeCli(_state).RunWithInput(Progress, "wake-gate");

        Assert.Equal((2, ""), (run.ExitCode, run.Stderr));
        var json = run.Json();
        Assert.Equal("escalated,reasonCode", Names(json));
        Assert.Equal((true, "no-provider-decided"), (json.GetProperty("escalated").GetBoolean(), json.GetProperty("reasonCode").GetString()));
        CliGolden.Match(run, "AskQuestion/Golden/escalated.json");
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("""{"agentType":"Explore","goal":"","acceptanceCriteria":"","proofCommand":""}""", "ready")]
    [InlineData("""{"agentType":"general-purpose","goal":"","acceptanceCriteria":"done","proofCommand":"make test"}""", "missing")]
    public async Task Brief_scope_prints_its_decision(string input, string decision)
    {
        var run = await new JudgeCli(_state).RunWithInput(input, "brief-scope");

        Assert.Equal((0, decision), (run.ExitCode, run.Json().GetProperty("decision").GetString()));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Stdin_that_isnt_the_input_record_is_the_invalid_input_escalation_and_is_recorded()
    {
        var run = await new JudgeCli(_state).RunWithInput("not json", "wake-gate");

        Assert.Equal((2, "invalid-input"), (run.ExitCode, run.Json().GetProperty("reasonCode").GetString()));
        Assert.Equal(["wake-gate|none|invalid-input|escalated"], await DecisionRows());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Stdin_with_a_repeated_key_is_the_invalid_input_escalation_and_is_recorded()
    {
        var run = await new JudgeCli(_state).RunWithInput("""{"text":"ok","text":"ok","senderKind":"teammate"}""", "wake-gate");

        Assert.Equal((2, "invalid-input", ""), (run.ExitCode, run.Json().GetProperty("reasonCode").GetString(), run.Stderr));
        Assert.Equal(["wake-gate|none|invalid-input|escalated"], await DecisionRows());
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("""{"text":"\ud800","senderKind":"teammate"}""")]
    [InlineData("""{"text":"ok","senderKind":"teammate","x":"\ud83d"}""")]
    [InlineData("""{"\ud800":1,"text":"ok","senderKind":"teammate"}""")]
    public async Task Stdin_with_a_lone_surrogate_escape_is_the_invalid_input_escalation_and_is_recorded(string input)
    {
        var run = await new JudgeCli(_state).RunWithInput(input, "wake-gate");

        Assert.Equal((2, "invalid-input", ""), (run.ExitCode, run.Json().GetProperty("reasonCode").GetString(), run.Stderr));
        Assert.Equal(["wake-gate|none|invalid-input|escalated"], await DecisionRows());
    }

    private async Task<List<string>> DecisionRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = new SqliteConnectionFactory(JudgeDatabase.OptionsFor(Path.Combine(_state.Path, "judge")));
        await using var connection = (await factory.Open(ct)).Value;
        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT question || '|' || provider || '|' || reason_code || '|' || outcome FROM decisions";
        await using var reader = await query.ExecuteReaderAsync(ct);
        List<string> rows = [];
        while (await reader.ReadAsync(ct))
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("nope", null)]
    [InlineData("wake-gate", "extra")]
    public async Task An_unknown_question_or_an_argument_is_a_usage_error(string name, string? argument)
    {
        var run = await new JudgeCli(_state).RunWithInput(Ack, argument is null ? [name] : [name, argument]);

        Assert.Equal((64, ""), (run.ExitCode, run.Stdout));
        Assert.StartsWith("usage: judge ", run.Stderr, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_question_disabled_in_the_config_escalates()
    {
        var cli = new JudgeCli(_state);
        Assert.Equal(0, (await cli.Run("config", "set", "wake-gate", "enabled", "false")).ExitCode);

        var run = await cli.RunWithInput(Ack, "wake-gate");

        Assert.Equal((2, "question-disabled"), (run.ExitCode, run.Json().GetProperty("reasonCode").GetString()));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("""{"transcriptTail":"Tests pass. Next I'll merge the PR."}""", null, 0)]
    [InlineData("""{"transcriptTail":"Next I'll run the tests"}""", "ask", 0)]
    [InlineData("""{"transcriptTail":"Next I'll run the tests"}""", "continue", 2)]
    [InlineData("""{"transcriptTail":"Next I'll run the tests"}""", null, 0)]
    [InlineData("not json", null, 0)]
    [InlineData("""{"transcriptTail":"x","transcriptTail":"Next I'll run the tests"}""", null, 0)]
    [InlineData("""{"transcriptTail":"x \ud83d"}""", null, 0)]
    [InlineData("""{"transcriptTail":"Next I'll run the tests","\udc00":"x"}""", null, 0)]
    public async Task Ask_check_exits_0_for_ask_or_an_escalation_and_2_for_continue(string input, string? model, int exitCode)
    {
        var cli = new JudgeCli(_state) { Providers = model is null ? null : [Cli(ContentClass.Transcript).Decides(model, 0.9)] };

        var run = await cli.RunWithInput(input, "ask-check");

        Assert.Equal((exitCode, ""), (run.ExitCode, run.Stderr));
        Assert.NotEmpty(run.Stdout);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_question_s_declared_extra_fields_ride_in_the_decided_envelope()
    {
        var extra = new JsonObject { ["chosenIndex"] = 1, ["note"] = "not declared" };
        var cli = new JudgeCli(_state) { Providers = [Cli(ContentClass.Code).Returns(new ProviderDecided("repaired", 0.9, "model") { Extra = extra })] };
        const string Input = """{"brokenSelector":"#save","step":"click save","candidates":[{"index":1,"role":"button","accessibleName":"Save","testId":null,"text":null}]}""";

        var run = await cli.RunWithInput(Input, "ui-element-repair");

        Assert.Equal(0, run.ExitCode);
        var json = run.Json();
        Assert.Equal("chosenIndex,confidence,decision,id,model,reasonCode", Names(json));
        Assert.Equal(("repaired", 1, "claude-cli"), (json.GetProperty("decision").GetString(), json.GetProperty("chosenIndex").GetInt32(), json.GetProperty("model").GetString()));
        CliGolden.Match(run, "AskQuestion/Golden/decided-with-extras.json");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_calling_session_comes_from_AW_SESSION_ID()
    {
        var cli = new JudgeCli(_state) { Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_SESSION_ID"] = "session-7" } };

        var id = (await cli.RunWithInput(Ack, "wake-gate")).Json().GetProperty("id").GetString();

        var row = await _state.DecisionLog().Find(DecisionId.From(id!), TestContext.Current.CancellationToken);   // why: a decided envelope has an id
        Assert.Equal(SessionId.From("session-7"), row.Value.Details?.SessionId);
    }

    public void Dispose() => _state.Dispose();
}
