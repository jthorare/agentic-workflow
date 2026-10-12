using Aw.Judge.Core.Ids;
using Aw.Judge.Infrastructure.Sqlite;

namespace Aw.Judge.Tests.SaveBrief;

// 'judge brief save' < input.json. The hook contract is the input's field names (scope-gate.sh writes them): eight
// required fields, every one a string (empty is fine) and dispatchName a string or null; other keys are ignored.
public sealed class SaveBriefCliTests : IDisposable
{
    private const string Payload =
        """{"toolUseId":"toolu_9","sessionId":"","promptId":"p","dispatchName":null,"subagentType":"Explore","goal":"","acceptanceCriteria":"a","proofCommand":"c","extra":1}""";

    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Brief_save_stores_the_payload_and_prints_nothing()
    {
        var run = await new JudgeCli(_state).RunWithInput(Payload, "brief", "save");

        Assert.Equal(new CliRun(0, "", ""), run);
        var saved = await new SqliteBriefStore(_state.Database()).ByToolUseId(ToolUseId.From("toolu_9"), CancellationToken.None);
        Assert.NotNull(saved.Value);
        Assert.Equal(("", "Explore", "", null), (saved.Value.SessionId.Value, saved.Value.SubagentType, saved.Value.Goal, saved.Value.DispatchName));
    }

    // Any stdin that doesn't deserialize into the input record is one outcome: exit 1 and one 'invalid input: …' line.
    [Theory, Trait("Lane", "integration")]
    [InlineData("")]
    [InlineData("{\"toolUseId\":")]
    [InlineData("\uFEFF{}")]
    [InlineData("""{"toolUseId":"t","sessionId":"s","promptId":"p","dispatchName":null,"subagentType":"x","goal":"g","acceptanceCriteria":"a"}""")]
    [InlineData("""{"toolUseId":"t","sessionId":"s","promptId":"p","subagentType":"x","goal":"g","acceptanceCriteria":"a","proofCommand":"c"}""")]
    [InlineData("""{"toolUseId":"t","sessionId":"s","promptId":"p","dispatchName":7,"subagentType":"x","goal":"g","acceptanceCriteria":"a","proofCommand":"c"}""")]
    [InlineData("""{"toolUseId":1,"sessionId":"s","promptId":"p","dispatchName":null,"subagentType":"x","goal":"g","acceptanceCriteria":"a","proofCommand":"c"}""")]
    [InlineData("""{"toolUseId":null,"sessionId":"s","promptId":"p","dispatchName":null,"subagentType":"x","goal":"g","acceptanceCriteria":"a","proofCommand":"c"}""")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"brief\"")]
    public async Task Brief_save_refuses_input_that_isnt_the_input_record(string stdin)
    {
        var run = await new JudgeCli(_state).RunWithInput(stdin, "brief", "save");

        Assert.Equal((1, ""), (run.ExitCode, run.Stdout));
        Assert.StartsWith("invalid input: ", run.Stderr, StringComparison.Ordinal);
        Assert.Equal(run.Stderr.Length - 1, run.Stderr.IndexOf('\n', StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Brief_save_refuses_an_id_past_its_cap()
    {
        var payload = Payload.Replace("toolu_9", new string('t', 5000), StringComparison.Ordinal);

        var run = await new JudgeCli(_state).RunWithInput(payload, "brief", "save");

        Assert.Equal(new CliRun(1, "", "invalid input: toolUseId is not a valid id\n"), run);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Brief_save_exits_1_when_the_tool_use_id_already_has_a_brief()
    {
        var cli = new JudgeCli(_state);
        _ = await cli.RunWithInput(Payload, "brief", "save");

        var run = await cli.RunWithInput(Payload, "brief", "save");

        Assert.Equal(new CliRun(1, "", "brief already saved: toolu_9\n"), run);
    }

    public void Dispose() => _state.Dispose();
}
