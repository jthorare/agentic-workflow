using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Prove;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Checks;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Worktrees;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;
using Aw.Sindri.Tests.Workflow.BuildShip;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Prove;

/// <summary>'step.prove.before-after' on the block runtime (line spec §7.3): a fix's recorded script fails at the phase's base and passes at the head, by the same script.</summary>
[Lane(TestLane.Unit)]
public sealed class ProveBeforeAfterStepTests
{
    private const string Head = "abcabcabcabcabcabc";
    private const string Base = "1111111111111111111111111111111111111111";
    private const string Address = "T-1/prove/1";
    private const string Dir = "/packets/p1";
    private const string Workspace = "/work/head";
    private const string BaseTree = "/run/T-1/prove/1/before-after/base";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class Rig(string[]? commands = null, string signals = """{"AC-1":[{"id":"SG-1a","kind":"test","statement":"s","runsAt":"base+head","script":"proof/ac1.sh"}]}""")
    {
        public InMemoryBriefFiles Files { get; } = new InMemoryBriefFiles()
                .With("acceptance.json", """{"schemaVersion":1,"items":[{"id":"AC-1","text":"one","status":"failing"},{"id":"AC-2","text":"two","status":"superseded"}]}""")
                .With("signals.json", $$$"""{"schemaVersion":1,"signals":{{{signals}}}}""")
                .With("phase.json", """{"kind":"fix"}""")
                .With("holdout.json", """{"schemaVersion":1,"setHash":"sha256:aa","count":2}""")
                .With("packet.json", $$$"""{"schemaVersion":1,"kind":"phase","id":"line-p1","repo":"owner/name","baseSha":"{{{Base}}}","status":"frozen","hash":null,"links":{}}""");

        public InMemoryRunArea Area { get; } = new();

        public InMemoryRepoCheckouts Checkouts { get; } = new InMemoryRepoCheckouts().With("name", "/checkouts/name");

        public FakeWorktrees Worktrees { get; } = new FakeWorktrees().WithRepo("/checkouts/name", Base);

        public FakeScriptRuns Scripts { get; } = new FakeScriptRuns().Exits(BaseTree, 1).Exits(Workspace, 0);

        public ProveBeforeAfterStep Step() => new(Files, Area, Checkouts, Worktrees, Scripts,
            new RepoCommandRunner(new StubCommandSource(commands ?? ["dotnet test --filter Fix"], TimeSpan.FromMinutes(5)), new InMemoryCheckCommands(), new StepRig.CountingSlots(), new FakeTimeProvider(KnownInstants.Epoch)));
    }

    private static StepContext Context(bool workspace = true, bool address = true, string? redTests = null) =>
        BuildShipContexts.Of("step.prove.before-after", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["phasePacket"] = $$$"""{"hash":"sha256:{{new string('b', 64)}}","dir":"{{{Dir}}}"}""",
            ["built"] = $$$"""{"head":"{{{Head}}}"}""",
            ["workspace"] = workspace ? $"\"{Workspace}\"" : "null",
            ["redTests"] = redTests ?? "{}",
        }) with
        { Address = address ? Address : null, Repo = RepoName.From("name"), };

    private static JsonNode Out(BlockStepResult result) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:beforeAfter")].GetRawText())!;

    [Fact]
    public async Task A_script_that_fails_at_the_base_and_passes_at_the_head_is_a_pass_recorded_with_the_same_script()
    {
        var rig = new Rig();

        var result = await rig.Step().Run(Context(), Token);

        Assert.Equal("pass", result.Result!.Outcome.Value);
        var fact = Out(result);
        Assert.Equal(Head, (string?)fact["commit"]);
        var row = Assert.Single(fact["results"]!.AsArray())!;
        Assert.Equal(("AC-1", "SG-1a", "fail", "pass", "proof/ac1.sh", "proof/ac1.sh"), ((string?)row["item"], (string?)row["signal"], (string?)row["base"], (string?)row["head"], (string?)row["baseScript"], (string?)row["headScript"]));
        Assert.All(rig.Scripts.Calls, static c => Assert.Equal("/work/head/proof/ac1.sh", c.Script));
        Assert.Equal([BaseTree, Workspace], rig.Scripts.Calls.Select(static c => c.Directory), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_script_that_already_passes_at_the_base_is_a_fail()
    {
        var rig = new Rig();
        _ = rig.Scripts.Exits(BaseTree, 0);

        var result = await rig.Step().Run(Context(), Token);

        Assert.Equal("fail", result.Result!.Outcome.Value);
        Assert.Equal("pass", (string?)Out(result)["results"]![0]!["base"]);
    }

    [Fact]
    public async Task A_script_that_still_fails_at_the_head_is_a_fail()
    {
        var rig = new Rig();
        _ = rig.Scripts.Exits(Workspace, 3);

        var result = await rig.Step().Run(Context(), Token);

        Assert.Equal("fail", result.Result!.Outcome.Value);
        Assert.Equal("fail", (string?)Out(result)["results"]![0]!["head"]);
    }

    [Fact]
    public async Task A_base_and_head_signal_with_no_recorded_script_is_recorded_missing_with_a_note_and_passes_without_running_anything()
    {
        var rig = new Rig(signals: """{"AC-1":[{"id":"SG-1a","kind":"test","statement":"s","runsAt":"base+head"}]}""");

        var result = await rig.Step().Run(Context(), Token);

        Assert.Equal("pass", result.Result!.Outcome.Value);
        Assert.Equal("missing", (string?)Out(result)["results"]![0]!["base"]);
        Assert.Contains("not runnable", (string?)Out(result)["results"]![0]!["note"], StringComparison.Ordinal);
        Assert.Empty(rig.Scripts.Calls);
    }

    [Fact]
    public async Task A_script_path_that_leaves_the_checkout_is_not_run()
    {
        var rig = new Rig(signals: """{"AC-1":[{"id":"SG-1a","kind":"test","statement":"s","runsAt":"base+head","script":"../../etc/x.sh"}]}""");

        var result = await rig.Step().Run(Context(), Token);

        Assert.Equal("missing", (string?)Out(result)["results"]![0]!["base"]);
        Assert.Empty(rig.Scripts.Calls);
    }

    [Fact]
    public async Task Head_only_signals_and_superseded_items_are_not_run_and_a_phase_with_nothing_to_run_passes_with_no_results()
    {
        var rig = new Rig(signals: """{"AC-1":[{"id":"SG-1a","kind":"test","statement":"s","runsAt":"head","script":"a.sh"}],"AC-2":[{"id":"SG-2a","kind":"test","statement":"s","runsAt":"base+head","script":"b.sh"}]}""");

        var result = await rig.Step().Run(Context(), Token);

        Assert.Equal("pass", result.Result!.Outcome.Value);
        Assert.Empty(Out(result)["results"]!.AsArray());
        Assert.Empty(rig.Scripts.Calls);
    }

    [Fact]
    public async Task A_script_that_cannot_run_fails_the_Step_so_the_instance_parks()
    {
        var rig = new Rig();
        _ = rig.Scripts.CannotRun(Workspace);

        var result = await rig.Step().Run(Context(), Token);

        Assert.Null(result.Result);
        Assert.Contains("SG-1a", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_scratch_worktree_is_made_at_the_phase_base_and_removed_afterwards()
    {
        var rig = new Rig();

        var result = await rig.Step().Run(Context(), Token);

        Assert.NotNull(result.Result);
        var again = await rig.Worktrees.Remove(BaseTree, Token);   // already gone: removing it again succeeds, and nothing is left to clean
        Assert.False(again.IsError);
        var recreated = await rig.Worktrees.Create(new Aw.Sindri.Core.Worktrees.WorktreeRequest("/checkouts/name", Base, "sindri/before-after/line-p1", BaseTree), Token);
        Assert.False(recreated.IsError);
    }

    [Fact]
    public async Task A_context_without_the_workspace_or_the_address_fails_the_Step()
    {
        var rig = new Rig();

        Assert.Null((await rig.Step().Run(Context(workspace: false), Token)).Result);
        Assert.Null((await rig.Step().Run(Context(address: false), Token)).Result);
        Assert.Empty(rig.Scripts.Calls);
    }

    private const string NoScript = """{"AC-1":[{"id":"SG-1a","kind":"test","statement":"s","runsAt":"base+head"}]}""";
    private const string RedTests = """{"commit":"abc1234","items":{"AC-1":{"tests":[{"id":"Fix_works","path":"tests/FixTests.cs"}]}}}""";
    private const string Generated = "/run/T-1/prove/1/before-after/AC-1.sh";

    [Fact]
    public async Task A_signal_with_no_script_runs_a_script_the_Step_writes_from_the_items_red_tests_and_the_profile_commands_at_base_and_head()
    {
        var rig = new Rig(signals: NoScript);

        var result = await rig.Step().Run(Context(redTests: RedTests), Token);

        Assert.Equal("pass", result.Result!.Outcome.Value);
        var row = Out(result)["results"]![0]!;
        Assert.Equal(("fail", "pass", "before-after/AC-1.sh", "before-after/AC-1.sh"), ((string?)row["base"], (string?)row["head"], (string?)row["baseScript"], (string?)row["headScript"]));
        Assert.Equal([(BaseTree, Generated), (Workspace, Generated)], rig.Scripts.Calls);
        var script = rig.Area.ReadLocation(Generated)!;
        Assert.Contains("tests/FixTests.cs", script, StringComparison.Ordinal);   // copied into the base tree, where the red test does not exist yet
        Assert.Contains("'dotnet' 'test' '--filter' 'Fix'", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_recorded_script_is_run_in_preference_to_the_red_tests()
    {
        var rig = new Rig();

        _ = await rig.Step().Run(Context(redTests: RedTests), Token);

        Assert.All(rig.Scripts.Calls, static c => Assert.Equal("/work/head/proof/ac1.sh", c.Script));
        Assert.Null(rig.Area.ReadLocation(Generated));
    }

    [Fact]
    public async Task A_signal_with_no_script_and_no_red_test_path_is_recorded_missing()
    {
        var rig = new Rig(signals: NoScript);

        var result = await rig.Step().Run(Context(redTests: """{"items":{"AC-1":{"appOnly":{"signal":"SG-1a"}}}}"""), Token);

        Assert.Equal("missing", (string?)Out(result)["results"]![0]!["base"]);
        Assert.Empty(rig.Scripts.Calls);
    }

    [Fact]
    public async Task A_red_test_path_that_leaves_the_checkout_is_not_used()
    {
        var rig = new Rig(signals: NoScript);

        var result = await rig.Step().Run(Context(redTests: """{"items":{"AC-1":{"tests":[{"id":"x","path":"../../etc/passwd"}]}}}"""), Token);

        Assert.Equal("missing", (string?)Out(result)["results"]![0]!["base"]);
        Assert.Empty(rig.Scripts.Calls);
    }

    [Fact]
    public async Task A_signal_with_red_tests_but_no_profile_commands_is_recorded_missing()
    {
        var rig = new Rig(commands: [], signals: NoScript);

        var result = await rig.Step().Run(Context(redTests: RedTests), Token);

        Assert.Equal("missing", (string?)Out(result)["results"]![0]!["base"]);
        Assert.Empty(rig.Scripts.Calls);
    }
}
