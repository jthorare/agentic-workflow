using System.Text.Json.Nodes;
using Aw.Judge.Core.Providers;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.ExplainDecision;

// #1248: a resolution-check decision keeps its reasons, and an answer that needs reasons but has none is not a decision.
public sealed class ExplainDecisionReasonsTests : IDisposable
{
    private const string Input =
        """{"brief":"Saving a draft loses the title","expected":"title kept","actual":"title lost","rootCause":"stale form state","checkKind":"test","checkSummary":"draft round trip","beforePassed":false,"afterPassed":true,"diffStat":"1 file changed"}""";

    private readonly TempStateDirectory _state = new();

    private static ScriptedProvider Model(string decision, params string[] reasons)
    {
        var answer = new ProviderDecided(decision, 0.9, "model")
        {
            Extra = reasons.Length == 0 ? null : new JsonObject { ["reasons"] = new JsonArray([.. reasons.Select(r => JsonValue.Create(r))]) },
        };
        return new ScriptedProvider(ProviderName.ClaudeCli, ContentClass.Brief).Returns(answer);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Why_returns_the_reasons_a_resolution_check_decision_was_stored_with()
    {
        var cli = new JudgeCli(_state) { Providers = [Model("partial", "the list view still drops the title", "no test covers rename")] };
        var asked = await cli.RunWithInput(Input, "resolution-check");
        Assert.Equal(0, asked.ExitCode);

        var run = await cli.Run("why", asked.Json().GetProperty("id").GetString()!);

        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        var reasons = run.Json().GetProperty("reasons").EnumerateArray().Select(r => r.GetString()).ToArray();
        Assert.Equal(["the list view still drops the title", "no test covers rename"], reasons);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("partial")]
    [InlineData("unresolved")]
    public async Task A_partial_or_unresolved_answer_without_reasons_is_escalated_and_stores_no_decision(string decision)
    {
        var cli = new JudgeCli(_state) { Providers = [Model(decision)] };

        var asked = await cli.RunWithInput(Input, "resolution-check");

        Assert.Equal(2, asked.ExitCode);
        Assert.True(asked.Json().GetProperty("escalated").GetBoolean());
        Assert.False(asked.Json().TryGetProperty("id", out _));
    }

    public void Dispose() => _state.Dispose();
}
