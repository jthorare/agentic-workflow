using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>
/// #1239: a lens that comes back degraded the same way after a regather is not gathered a third time. The check reports 'stalled' with the lens, the source, its code and
/// the fix written into the findings; evidence reports 'converged' on its first round when only those findings fail, so the owner decides with the packet in front of them.
/// </summary>
public sealed class ScopingStalledLensTests
{
    private const string Cov2 = "SCP-COV-002";
    private const string Eval = "sindri/evals/workflow.scoping/stalled-lens-converged.jsonl";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The gold facts with one source of one lens turned degraded (code SND-CONTEXT-048, no other change, so only SCP-COV-002 fails).</summary>
    private static ScopingFacts Degraded(string lens, string source) => ScopingFacts.Expiry().Edit("coverage", n =>
    {
        var row = n["lenses"]!.AsArray().Single(r => r!["lens"]!.GetValue<string>() == lens)!;   // why: the gold coverage has a row per lens
        var one = row["searched"]!["sources"]!.AsArray().Single(s => s!["id"]!.GetValue<string>() == source)!;   // why: the gold rows list their sources
        one["state"] = "degraded";
        one["code"] = "SND-CONTEXT-048";
    });

    private static async Task<BlockStepResult> Check(InMemoryRunArea area, ScopingFacts facts) =>
        await new ScopeCheckStep(area).Run(ScopingStepContexts.Of("step.scope.check", facts), Token);

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;   // why: a completed result has outputs

    private static ScopingDeskResponse Evidence(ScopingFacts facts)
    {
        var ran = DeskBlocks.Run("step.scope.evidence", new ScopeFacts(facts));
        Assert.False(ran.IsError, ran.IsError ? ran.Error.Message : null);
        return ran.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_first_degraded_check_still_regathers_and_the_same_degrade_again_is_stalled_with_a_named_reason()
    {
        var area = new InMemoryRunArea();
        var first = await Check(area, Degraded("history", "git"));
        var again = Degraded("history", "git");
        again["roundLog"] = Out(first, "roundLog").ToJsonString();

        var second = await Check(area, again);

        Assert.Equal("regather", first.Result!.Outcome.Value);
        Assert.Equal("stalled", second.Result!.Outcome.Value);
        var finding = Assert.Single(Out(second, "findings").AsArray())!;   // why: one lens is degraded, so one finding
        Assert.Equal(Cov2, finding["rule"]!.GetValue<string>());
        Assert.Equal("history", finding["item"]!.GetValue<string>());
        Assert.Contains("git", finding["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("SND-CONTEXT-048", finding["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.NotEmpty(finding["fix"]!.GetValue<string>());
        Assert.DoesNotContain("deadline", finding["fix"]!.GetValue<string>(), StringComparison.Ordinal);   // why: the history read has no timer since #1221, so no deadline is the cause
        Assert.Contains(Out(second, "roundLog").AsArray(), static e => e!["kind"]!.GetValue<string>() == "stalled" && e["key"]!.GetValue<string>() == $"{Cov2}:history");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_different_lens_degraded_the_second_time_is_not_stalled()
    {
        var area = new InMemoryRunArea();
        var first = await Check(area, Degraded("history", "git"));
        var other = Degraded("code", "repo");
        other["roundLog"] = Out(first, "roundLog").ToJsonString();

        var second = await Check(area, other);

        Assert.Equal("regather", second.Result!.Outcome.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evidence_with_only_the_stalled_findings_failing_is_converged_on_its_first_round()
    {
        var facts = Degraded("history", "git");
        facts["roundLog"] = new JsonArray(new JsonObject
        {
            ["round"] = 2,
            ["step"] = "check",
            ["kind"] = "stalled",
            ["key"] = $"{Cov2}:history",
            ["text"] = "stalled",
            ["by"] = "step.scope.check",
        }).ToJsonString();

        var response = Evidence(facts);

        Assert.Equal("converged", response.Outcome);
        Assert.Contains("a lens stayed degraded", response.Outputs["recommendation"], StringComparison.Ordinal);
        Assert.Contains(response.Findings, static f => f.Rule == Cov2 && f.Item == "history");
    }

    [Fact, Trait("Lane", "unit")]
    public void Evidence_with_a_degraded_lens_nobody_stalled_on_still_regathers() =>
        Assert.Equal("regather", Evidence(Degraded("history", "git")).Outcome);

    [Fact, Trait("Lane", "unit")]
    public void A_stalled_check_goes_on_to_the_size_and_the_evidence_converges_at_the_stop_without_parking()
    {
        var read = EvalCaseReader.Read("stalled-lens", ShippedRecipes.Read(Eval), EvalComposition.Options);
        var evalCase = Assert.IsType<EvalCase>(read.Case);
        var recipe = EvalComposition.Recipe("sindri/workflow.scoping.yaml");

        var verdict = EvalRunner.Run(evalCase, recipe, EvalComposition.Catalog, InMemoryChildRecipes.None);

        Assert.Null(verdict.Detail);
    }
}
