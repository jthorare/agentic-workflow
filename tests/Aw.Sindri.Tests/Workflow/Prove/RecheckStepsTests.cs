using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Prove;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.Prove;

/// <summary>The re-proof child's Steps on the block runtime (PB spec §8): plan, rerun-scripts and log.</summary>
[Lane(TestLane.Unit)]
public sealed class RecheckStepsTests
{
    private const string Address = "T-1/pr-babysitter/1/prove-recheck/1";
    private const string Pack = "/packs/p1";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;

    private static StepContext PlanContext(string? handoff, string at = """{"head":{"sha":"h2"}}""") => With(
        BuildShipContexts.Of("step.recheck.plan", []),
        ("handoff", handoff), ("at", at), ("workspace", "\"/work/p1\""));

    private static StepContext With(StepContext context, params (string Name, string? Value)[] arguments) => context with
    {
        Address = Address,
        Arguments = arguments.Where(static a => a.Value is not null).ToDictionary(static a => a.Name, static a => JsonSerializer.SerializeToElement(a.Value), StringComparer.Ordinal),
    };

    private static StepContext Facts(string step, params (string Key, string Json)[] facts) =>
        BuildShipContexts.Of(step, facts.ToDictionary(static f => f.Key, static f => f.Json, StringComparer.Ordinal)) with { Address = Address };

    private static InMemoryBriefFiles ProofPackFiles() => new InMemoryBriefFiles()
        .With("before-after.json", """{"results":[{"item":"AC-1","signal":"SG-1","base":"fail","head":"pass","baseScript":"proof/ac1.sh","headScript":"proof/ac1.sh"}]}""")
        .With("proof.json", """{"items":[{"id":"AC-1","artifacts":[{"script":"proof/ac1.sh"},{"script":"proof/old.sh","superseded":true},{"script":"proof/ac2.sh"}]}]}""");

    // Per-script exits, which FakeScriptRuns (one exit per directory) can't give.
    private sealed class FakeScripts(params string[] failing) : IScriptRuns
    {
        public List<string> Ran { get; } = [];

        public ValueTask<int?> Run(string directory, string script, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Ran.Add(directory + ":" + script);
            return ValueTask.FromResult<int?>(failing.Contains(script, StringComparer.Ordinal) ? 1 : 0);
        }
    }

    [Fact]
    public async Task Plan_with_no_hand_off_has_nothing_to_prove_again()
    {
        var result = await new RecheckPlanStep(ProofPackFiles(), new InMemoryRunArea()).Run(PlanContext(handoff: null), Token);

        Assert.Equal("nothing", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task Plan_lists_each_live_recorded_script_once_on_the_workspace_at_the_head()
    {
        var result = await new RecheckPlanStep(ProofPackFiles(), new InMemoryRunArea()).Run(PlanContext($$$"""{"hash":"sha256:aa","proofPack":{"dir":"{{{Pack}}}"}}"""), Token);

        Assert.Equal("planned", result.Result!.Outcome.Value);
        var plan = Out(result, "recheckPlan");
        Assert.Equal("h2", (string?)plan["headSha"]);
        Assert.Equal("/work/p1", (string?)plan["workspace"]);
        Assert.Equal(["proof/ac1.sh", "proof/ac2.sh"], plan["scripts"]!.AsArray().Select(static s => (string?)s), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Plan_fails_when_the_proof_pack_cannot_be_read()
    {
        var result = await new RecheckPlanStep(new InMemoryBriefFiles(), new InMemoryRunArea()).Run(PlanContext($$$"""{"proofPack":{"dir":"{{{Pack}}}"}}"""), Token);

        Assert.Null(result.Result);
    }

    [Fact]
    public async Task Rerun_passes_when_every_script_passes_and_records_no_finding()
    {
        var scripts = new FakeScripts();
        var plan = """{"headSha":"h2","workspace":"/work/p1","scripts":["proof/ac1.sh","proof/ac2.sh"]}""";

        var result = await new ProveRerunScriptsStep(scripts, new InMemoryRunArea()).Run(Facts("step.prove.rerun-scripts", ("recheckPlan", plan)), Token);

        Assert.Equal("pass", result.Result!.Outcome.Value);
        Assert.Empty(Out(result, "findings").AsArray());
        Assert.Equal(["/work/p1:proof/ac1.sh", "/work/p1:proof/ac2.sh"], scripts.Ran);
    }

    [Fact]
    public async Task Rerun_fails_with_a_finding_for_each_script_that_does_not_pass()
    {
        var plan = """{"headSha":"h2","workspace":"/work/p1","scripts":["proof/ac1.sh","proof/ac2.sh"]}""";

        var result = await new ProveRerunScriptsStep(new FakeScripts("proof/ac2.sh"), new InMemoryRunArea()).Run(Facts("step.prove.rerun-scripts", ("recheckPlan", plan)), Token);

        Assert.Equal("fail", result.Result!.Outcome.Value);
        var finding = Assert.Single(Out(result, "findings").AsArray());
        Assert.Equal("proof/ac2.sh", (string?)finding!["item"]);
    }

    [Fact]
    public async Task Rerun_fails_a_script_that_could_not_be_run()
    {
        var plan = """{"headSha":"h2","workspace":"/work/p1","scripts":["proof/ac1.sh"]}""";

        var result = await new ProveRerunScriptsStep(new FakeScriptRuns().CannotRun("/work/p1"), new InMemoryRunArea()).Run(Facts("step.prove.rerun-scripts", ("recheckPlan", plan)), Token);

        Assert.Equal("fail", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task Rerun_refuses_a_script_outside_the_workspace_without_running_it()
    {
        var scripts = new FakeScriptRuns();
        var plan = """{"headSha":"h2","workspace":"/work/p1","scripts":["../evil.sh","/abs.sh"]}""";

        var result = await new ProveRerunScriptsStep(scripts, new InMemoryRunArea()).Run(Facts("step.prove.rerun-scripts", ("recheckPlan", plan)), Token);

        Assert.Equal("fail", result.Result!.Outcome.Value);
        Assert.Empty(scripts.Calls);
        Assert.Equal(2, Out(result, "findings").AsArray().Count);
    }

    [Fact]
    public async Task Log_holds_when_the_plan_ran_and_every_script_passed()
    {
        var area = new InMemoryRunArea();
        _ = await new RecheckPlanStep(ProofPackFiles(), area).Run(PlanContext($$$"""{"proofPack":{"dir":"{{{Pack}}}"}}"""), Token);
        var plan = """{"headSha":"h2","workspace":"/work/p1","scripts":["proof/ac1.sh","proof/ac2.sh"]}""";
        _ = await new ProveRerunScriptsStep(new FakeScripts(), area).Run(Facts("step.prove.rerun-scripts", ("recheckPlan", plan)), Token);

        var result = await new RecheckLogStep(area).Run(Facts("step.recheck.log"), Token);

        Assert.Equal("held", result.Result!.Outcome.Value);
        var recheck = Out(result, "recheck");
        Assert.Equal("h2", (string?)recheck["headSha"]);
        Assert.Equal("held", (string?)recheck["verdict"]);
        Assert.Equal(2, recheck["scripts"]!.AsArray().Count);
    }

    [Fact]
    public async Task Log_is_broken_when_a_script_failed()
    {
        var area = new InMemoryRunArea();
        _ = await new RecheckPlanStep(ProofPackFiles(), area).Run(PlanContext($$$"""{"proofPack":{"dir":"{{{Pack}}}"}}"""), Token);
        var plan = """{"headSha":"h2","workspace":"/work/p1","scripts":["proof/ac1.sh"]}""";
        _ = await new ProveRerunScriptsStep(new FakeScripts("proof/ac1.sh"), area).Run(Facts("step.prove.rerun-scripts", ("recheckPlan", plan)), Token);

        var result = await new RecheckLogStep(area).Run(Facts("step.recheck.log"), Token);

        Assert.Equal("broken", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task Log_is_broken_when_the_plan_was_made_but_the_scripts_never_ran()
    {
        var area = new InMemoryRunArea();
        _ = await new RecheckPlanStep(ProofPackFiles(), area).Run(PlanContext($$$"""{"proofPack":{"dir":"{{{Pack}}}"}}"""), Token);

        var result = await new RecheckLogStep(area).Run(Facts("step.recheck.log"), Token);

        Assert.Equal("broken", result.Result!.Outcome.Value);   // the verify journey did not resolve, so the scripts were skipped
    }

    [Fact]
    public async Task Log_is_nothing_when_the_plan_found_no_hand_off_and_still_records_the_head()
    {
        var area = new InMemoryRunArea();
        _ = await new RecheckPlanStep(ProofPackFiles(), area).Run(PlanContext(handoff: null), Token);

        var result = await new RecheckLogStep(area).Run(Facts("step.recheck.log"), Token);

        Assert.Equal("nothing", result.Result!.Outcome.Value);
        Assert.Equal("h2", (string?)Out(result, "recheck")["headSha"]);
    }

    [Fact]
    public async Task Log_is_nothing_when_no_plan_was_made()
    {
        var result = await new RecheckLogStep(new InMemoryRunArea()).Run(Facts("step.recheck.log"), Token);

        Assert.Equal("nothing", result.Result!.Outcome.Value);
    }
}
