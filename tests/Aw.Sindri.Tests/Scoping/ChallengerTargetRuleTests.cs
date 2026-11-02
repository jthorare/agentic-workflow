using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>SCP-CHAL-002 compares the launcher agent each engine run recorded ('target' on the run record) before any model; the same agent needs a different model (#1008).</summary>
public sealed class ChallengerTargetRuleTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task A_challenger_on_another_agent_passes_even_when_no_model_is_recorded()
    {
        var response = await ScopingCheckTests.Run(Runs(drafter: "claude-code", challenger: "codex"), ScopeStage.Evidence);

        Assert.Equal(ScopeRuleStatus.Pass, response.Rules.Single(r => r.Rule == "SCP-CHAL-002").Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_challenger_on_another_agent_passes_even_when_the_model_strings_match()
    {
        var response = await ScopingCheckTests.Run(Runs(drafter: "claude-code", challenger: "codex", model: "same"), ScopeStage.Evidence);

        Assert.Equal(ScopeRuleStatus.Pass, response.Rules.Single(r => r.Rule == "SCP-CHAL-002").Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_challenger_on_the_drafters_agent_and_model_fails_the_rule()
    {
        var response = await ScopingCheckTests.Run(Runs(drafter: "codex", challenger: "codex", model: "same"), ScopeStage.Evidence);

        Assert.Equal(ScopeRuleStatus.Fail, response.Rules.Single(r => r.Rule == "SCP-CHAL-002").Status);
    }

    /// <summary>The gold facts whose two agent runs name a launcher agent and, when <paramref name="model"/> is given, that model.</summary>
    private static ScopingFacts Runs(string drafter, string challenger, string? model = null) => ScopingFacts.Expiry().Edit("runs", n =>
    {
        var runs = n["runs"]!.AsArray();
        foreach (var (run, target) in new[] { (runs[0]!, drafter), (runs[1]!, challenger) })
        {
            run["target"] = target;
            _ = run.AsObject().Remove("model");
            if (model is not null)
            {
                run["model"] = model;
            }
        }
    });
}
