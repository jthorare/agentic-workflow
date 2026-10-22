using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Prove;
using Aw.Sindri.Testing.Prove;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.Prove;

/// <summary>The prove station's holdout-log Step on the block runtime (line spec §7.3, §9.3).</summary>
[Lane(TestLane.Unit)]
public sealed class ProveHoldoutLogStepTests
{
    private const string Address = "T-1/line/1/phase.1/prove.1";
    private const string Line = "T-1";
    private const string Phase = "p1";
    private const string Checkout = "/checkouts/name";
    private const string Base = "b00b";
    private const string Head = "abc";
    private const string Canary = "violet otter ledger 4417";

    private const string SetText = """{ "schemaVersion": 1, "phase": "p1", "scenarios": [ { "id": "P1-H1", "item": "AC-1", "kind": "api", "steps": ["open an expired link from an export, then read the response status and the body"], "expected": "the server answers with status 410 and says the link is gone" }, { "id": "P1-H2", "item": "AC-2", "kind": "ui", "steps": ["click save twice"], "expected": "one record" }, { "id": "P1-C", "item": null, "kind": "canary", "steps": [], "expected": "never", "phrase": "violet otter ledger 4417" } ] }""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;

    private static JsonObject Verdict(string scenario, string item, string verdict, string observed) =>
        new() { ["scenario"] = scenario, ["item"] = item, ["verdict"] = verdict, ["category"] = "api", ["observed"] = observed };

    private static Dictionary<string, string> Facts(params JsonObject[] results) => new(StringComparer.Ordinal)
    {
        ["holdoutResult"] = new JsonObject { ["commit"] = Head, ["results"] = new JsonArray([.. results]) }.ToJsonString(),
        ["built"] = $$"""{"head":"{{Head}}","commits":[]}""",
        ["phasePacket"] = """{"hash":"sha256:aa","dir":"/packets/p1"}""",
    };

    private static Dictionary<string, string> BothPass() => Facts(Verdict("P1-H1", "AC-1", "pass", "the link answered 410"), Verdict("P1-H2", "AC-2", "pass", "one record was saved"));

    private static StepContext Context(Dictionary<string, string> facts, bool address = true, bool repo = true) =>
        BuildShipContexts.Of("step.prove.holdout-log", facts) with { Address = address ? Address : null, Repo = repo ? RepoName.From("name") : null };

    private sealed class Rig
    {
        public InMemoryHoldoutStore Store { get; } = new();

        public FakeCommitText Commits { get; } = new();

        public InMemoryBriefFiles Files { get; } = new InMemoryBriefFiles().With("packet.json", $$"""{"id":"{{Phase}}","repo":"owner/name","baseSha":"{{Base}}"}""");

        public InMemoryRepoCheckouts Checkouts { get; } = new InMemoryRepoCheckouts().With("name", Checkout);

        public Rig()
        {
            Store.Put(Line, Phase, SetText);
        }

        public ProveHoldoutLogStep Step() => new(Store, Commits, Files, Checkouts);
    }

    [Fact]
    public async Task Passing_scenarios_with_a_clean_canary_report_passed_and_record_the_canary_result()
    {
        var rig = new Rig();

        var result = await rig.Step().Run(Context(BothPass()), Token);

        Assert.Equal("passed", result.Result!.Outcome.Value);
        Assert.Equal(["holdoutResult"], result.Result.Outputs.Keys.Select(static k => k.Value[5..]), StringComparer.Ordinal);
        var recorded = Out(result, "holdoutResult");
        Assert.Equal<(string?, string?)>((Head, "clean"), ((string?)recorded["commit"], (string?)recorded["canary"]));
        Assert.Equal(["P1-H1", "P1-H2", "P1-C"], recorded["results"]!.AsArray().Select(static r => (string?)r!["scenario"]), StringComparer.Ordinal);
        Assert.Empty((await rig.Store.Spent(Line, Phase, Token)).Value);
    }

    [Fact]
    public async Task The_canary_result_has_no_item_and_kind_canary_so_it_is_not_a_hidden_scenario()
    {
        var result = await new Rig().Step().Run(Context(BothPass()), Token);

        var canary = Out(result, "holdoutResult")["results"]!.AsArray().Single(static r => (string?)r!["scenario"] == "P1-C")!;
        Assert.Equal<(string?, string?)>(("canary", null), ((string?)canary["kind"], (string?)canary["item"]));
        Assert.Equal(["AC-1", "AC-2"], ProofChecks.HiddenResults(Out(result, "holdoutResult")).Select(static r => (string?)r["item"]), StringComparer.Ordinal);   // PRV-HOLD-001 counts these only
    }

    [Fact]
    public async Task A_failed_scenario_is_marked_spent_and_becomes_a_finding_in_terms_of_its_item()
    {
        var rig = new Rig();
        var facts = Facts(Verdict("P1-H1", "AC-1", "pass", "the link answered 410"), Verdict("P1-H2", "AC-2", "fail", "saving twice made two records"));

        var result = await rig.Step().Run(Context(facts), Token);

        Assert.Equal("failed", result.Result!.Outcome.Value);
        Assert.Equal("P1-H2", Assert.Single((await rig.Store.Spent(Line, Phase, Token)).Value));
        var results = Out(result, "holdoutResult")["results"]!.AsArray();
        Assert.Equal<bool?>([null, true], results.Where(static r => (string?)r!["kind"] != "canary").Select(static r => (bool?)r!["spent"]));
        var finding = Assert.Single(Out(result, "findings").AsArray())!;
        Assert.Equal<(string?, string?)>(("PRV-HOLD-001", "AC-2"), ((string?)finding["rule"], (string?)finding["item"]));
        Assert.Contains("saving twice made two records", (string?)finding["message"], StringComparison.Ordinal);
        Assert.DoesNotContain("P1-H2", finding.ToJsonString(), StringComparison.Ordinal);
        Assert.DoesNotContain("click save twice", finding.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_scenario_spent_earlier_is_flagged_spent_in_the_record()
    {
        var rig = new Rig();
        _ = await rig.Store.MarkSpent(Line, Phase, ["P1-H1"], Token);

        var result = await rig.Step().Run(Context(BothPass()), Token);

        Assert.Equal("passed", result.Result!.Outcome.Value);
        Assert.Equal<bool?>([true, null], Out(result, "holdoutResult")["results"]!.AsArray().Where(static r => (string?)r!["kind"] != "canary").Select(static r => (bool?)r!["spent"]));
    }

    [Fact]
    public async Task The_canary_in_the_commits_is_leaked_and_the_finding_never_names_the_token()
    {
        var rig = new Rig();
        _ = rig.Commits.With(Base, Head, $"fix: expired links\n\n+ // {Canary}\n");

        var result = await rig.Step().Run(Context(BothPass()), Token);

        Assert.Equal("leaked", result.Result!.Outcome.Value);
        Assert.Equal(["findings"], result.Result.Outputs.Keys.Select(static k => k.Value[5..]), StringComparer.Ordinal);
        var finding = Assert.Single(Out(result, "findings").AsArray())!;
        Assert.Equal<(string?, string?)>(("PRV-HOLD-003", null), ((string?)finding["rule"], (string?)finding["item"]));
        Assert.DoesNotContain(Canary, finding.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty((await rig.Store.Spent(Line, Phase, Token)).Value);
    }

    [Fact]
    public async Task The_canary_in_a_fact_the_Step_reads_is_leaked()
    {
        var facts = Facts(Verdict("P1-H1", "AC-1", "pass", $"the page showed {Canary}"), Verdict("P1-H2", "AC-2", "pass", "one record was saved"));

        var result = await new Rig().Step().Run(Context(facts), Token);

        Assert.Equal("leaked", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task The_scan_asks_for_the_commits_from_the_packets_base_to_the_built_head_in_the_repos_checkout()
    {
        var rig = new Rig();

        _ = await rig.Step().Run(Context(BothPass()), Token);

        Assert.Equal([(Checkout, Base, Head)], rig.Commits.Asked);
    }

    [Fact]
    public async Task An_observed_line_sharing_eight_words_with_its_scenario_parks_the_Step_and_records_nothing()
    {
        var rig = new Rig();
        var facts = Facts(Verdict("P1-H1", "AC-1", "pass", "I saw: The server answers with status 410 and says the link"), Verdict("P1-H2", "AC-2", "pass", "one record was saved"));

        var result = await rig.Step().Run(Context(facts), Token);

        Assert.Null(result.Result);
        Assert.Contains("AC-1", result.Failure, StringComparison.Ordinal);
        Assert.DoesNotContain("status 410", result.Failure, StringComparison.Ordinal);
        Assert.Empty((await rig.Store.Spent(Line, Phase, Token)).Value);
    }

    [Fact]
    public async Task An_observed_line_sharing_only_seven_words_is_accepted()
    {
        var facts = Facts(Verdict("P1-H1", "AC-1", "pass", "the server answers with status 410 and then closed"), Verdict("P1-H2", "AC-2", "pass", "one record was saved"));

        var result = await new Rig().Step().Run(Context(facts), Token);

        Assert.Equal("passed", result.Result!.Outcome.Value);
    }

    [Theory]
    [InlineData("holdoutResult")]
    [InlineData("built")]
    [InlineData("phasePacket")]
    public async Task A_missing_fact_fails_the_Step_and_names_it(string missing)
    {
        var facts = BothPass();
        _ = facts.Remove(missing);

        var result = await new Rig().Step().Run(Context(facts), Token);

        Assert.Null(result.Result);
        Assert.Contains(missing, result.Failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task A_context_without_an_address_or_a_repo_fails_the_Step(bool address, bool repo)
    {
        var result = await new Rig().Step().Run(Context(BothPass(), address, repo), Token);

        Assert.Null(result.Result);
        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task A_phase_with_no_hidden_set_fails_the_Step()
    {
        var rig = new Rig();
        var bare = new ProveHoldoutLogStep(new InMemoryHoldoutStore(), rig.Commits, rig.Files, rig.Checkouts);

        var result = await bare.Run(Context(BothPass()), Token);

        Assert.Null(result.Result);
        Assert.Contains("hidden set", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_set_without_a_canary_fails_the_Step_because_nothing_could_be_scanned_for()
    {
        var rig = new Rig();
        rig.Store.Put(Line, Phase, """{ "schemaVersion": 1, "phase": "p1", "scenarios": [ { "id": "P1-H1", "item": "AC-1", "kind": "api", "steps": ["a"], "expected": "b" }, { "id": "P1-H2", "item": "AC-2", "kind": "ui", "steps": ["c"], "expected": "d" } ] }""");

        var result = await rig.Step().Run(Context(BothPass()), Token);

        Assert.Null(result.Result);
        Assert.Contains("canary", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_report_naming_a_scenario_the_set_does_not_hold_fails_the_Step()
    {
        var facts = Facts(Verdict("P9-H9", "AC-1", "pass", "fine"));

        var result = await new Rig().Step().Run(Context(facts), Token);

        Assert.Null(result.Result);
        Assert.Contains("P9-H9", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Commits_that_cannot_be_read_fail_the_Step_rather_than_pass_an_unscanned_phase()
    {
        var rig = new Rig();
        rig.Commits.Unreadable = true;

        var result = await rig.Step().Run(Context(BothPass()), Token);

        Assert.Null(result.Result);
        Assert.Contains("commits", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_store_that_cannot_be_written_fails_the_Step_when_a_scenario_failed()
    {
        var rig = new Rig();
        rig.Store.Unwritable = true;
        var facts = Facts(Verdict("P1-H1", "AC-1", "fail", "nope"), Verdict("P1-H2", "AC-2", "pass", "one record was saved"));

        var result = await rig.Step().Run(Context(facts), Token);

        Assert.Null(result.Result);
        Assert.NotNull(result.Failure);
    }
}
