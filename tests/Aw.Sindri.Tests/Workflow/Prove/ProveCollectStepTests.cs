using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Prove;
using Aw.Sindri.Testing.Prove;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Prove;

/// <summary>'step.prove.collect' on the block runtime (BS-13): the files the evidence children name are hashed by Sindri and appended to fact:proof on the head.</summary>
[Lane(TestLane.Unit)]
public sealed class ProveCollectStepTests
{
    private const string Head = "abcabcabcabcabcabc";
    private const string Old = "1111111111111111";
    private const string Work = "/work/p1";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset _now = new(2026, 10, 22, 3, 0, 0, TimeSpan.Zero);

    private static string Proof(string artifactsOfI1 = "[]") =>
        $$"""{"head":"{{Head}}","items":[{"id":"i1","text":"one","signals":[{"id":"SG-1a"}],"artifacts":{{artifactsOfI1}}},{"id":"i2","text":"two","signals":[{"id":"SG-2a"}],"artifacts":[]}]}""";

    private static Dictionary<string, string> Facts(string? manual = null, string? pack = null, string? summary = null, string? proof = null)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["proof"] = proof ?? Proof(),
            ["built"] = $$"""{"head":"{{Head}}"}""",
            ["workspace"] = Work,
        };
        if (manual is not null)
        {
            facts["manualTest"] = manual;
        }

        if (pack is not null)
        {
            facts["evidencePack"] = pack;
        }

        if (summary is not null)
        {
            facts["evidenceSummary"] = summary;
        }

        return facts;
    }

    private static Task<BlockStepResult> Run(InMemoryArtifactFiles files, Dictionary<string, string> facts) =>
        new ProveCollectStep(files, new FakeTimeProvider(_now)).Run(BuildShipContexts.Of("step.prove.collect", facts), Token).AsTask();

    private static JsonArray Artifacts(BlockStepResult result, string item) =>
        (JsonArray)JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:proof")].GetRawText())!["items"]!.AsArray().OfType<JsonObject>().Single(i => (string?)i["id"] == item)["artifacts"]!;

    [Fact]
    public async Task A_file_with_no_item_or_signal_backs_every_item_with_a_hash_the_step_computed()
    {
        var files = new InMemoryArtifactFiles().With(Work, "evidence/walk.md", "walked it");

        var result = await Run(files, Facts(manual: """{"artifacts":["evidence/walk.md"]}"""));

        Assert.Equal("collected", result.Result!.Outcome.Value);
        foreach (var item in new[] { "i1", "i2" })
        {
            var artifact = (JsonObject)Artifacts(result, item).Single()!;
            Assert.Equal("evidence/walk.md", (string?)artifact["path"]);
            Assert.Equal(InMemoryArtifactFiles.HashOf("walked it"), (string?)artifact["sha256"]);
            Assert.Equal(Head, (string?)artifact["commit"]);
            Assert.Equal("app-run", (string?)artifact["kind"]);
            Assert.True((bool?)artifact["passing"]);
            Assert.Equal(_now.ToString("O"), (string?)artifact["at"]);
        }
    }

    [Fact]
    public async Task A_hash_or_commit_the_agent_typed_is_ignored()
    {
        var files = new InMemoryArtifactFiles().With(Work, "a.png", "pixels");

        var result = await Run(files, Facts(pack: """{"artifacts":[{"path":"a.png","sha256":"sha256:fake","commit":"deadbeef"}]}"""));

        var artifact = (JsonObject)Artifacts(result, "i1").Single()!;
        Assert.Equal(InMemoryArtifactFiles.HashOf("pixels"), (string?)artifact["sha256"]);
        Assert.Equal(Head, (string?)artifact["commit"]);
        Assert.Equal("verify", (string?)artifact["kind"]);
    }

    [Fact]
    public async Task An_item_or_a_signal_narrows_which_items_the_file_backs()
    {
        var files = new InMemoryArtifactFiles().With(Work, "i1.txt", "one").With(Work, "sg2.txt", "two");

        var result = await Run(files, Facts(summary: """{"artifacts":[{"path":"i1.txt","item":"i1","kind":"api-run","signal":"SG-1a"},{"path":"sg2.txt","signal":"SG-2a"}]}"""));

        var first = (JsonObject)Artifacts(result, "i1").Single()!;
        Assert.Equal("api-run", (string?)first["kind"]);
        Assert.Equal("SG-1a", (string?)first["signal"]);
        Assert.Equal("sg2.txt", (string?)((JsonObject)Artifacts(result, "i2").Single()!)["path"]);
    }

    [Fact]
    public async Task A_combined_summary_is_read_in_its_ui_api_and_db_parts()
    {
        var files = new InMemoryArtifactFiles().With(Work, "ui.png", "u").With(Work, "api.json", "a");

        var result = await Run(files, Facts(summary: """{"kind":"combined","ui":{"artifacts":["ui.png"]},"api":{"artifacts":["api.json"]}}"""));

        Assert.Equal(["ui.png", "api.json"], Artifacts(result, "i1").Select(a => (string?)a!["path"]), StringComparer.Ordinal);
        Assert.Equal(["ui-evidence", "api-evidence"], Artifacts(result, "i1").Select(a => (string?)a!["kind"]), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_child_entry_that_says_it_did_not_pass_is_recorded_as_not_passing()
    {
        var files = new InMemoryArtifactFiles().With(Work, "bad.txt", "x");

        var result = await Run(files, Facts(manual: """{"artifacts":[{"path":"bad.txt","passing":false}]}"""));

        Assert.False((bool?)((JsonObject)Artifacts(result, "i1").Single()!)["passing"]);
    }

    [Fact]
    public async Task A_missing_file_adds_nothing_so_the_evidence_step_names_the_item()
    {
        var result = await Run(new InMemoryArtifactFiles(), Facts(manual: """{"artifacts":["evidence/gone.md"]}"""));

        Assert.Equal("collected", result.Result!.Outcome.Value);
        Assert.Empty(Artifacts(result, "i1"));
        Assert.Empty(Artifacts(result, "i2"));
    }

    [Fact]
    public async Task A_path_outside_the_worktree_adds_nothing()
    {
        var files = new InMemoryArtifactFiles().With("/work", "p2/secret.txt", "s");

        var result = await Run(files, Facts(manual: """{"artifacts":["../p2/secret.txt"]}"""));

        Assert.Empty(Artifacts(result, "i1"));
    }

    [Fact]
    public async Task A_fact_that_names_no_files_leaves_the_proof_as_it_was()
    {
        var result = await Run(new InMemoryArtifactFiles(), Facts(manual: "walked it by hand", summary: """{"kind":"none"}"""));

        Assert.Equal("collected", result.Result!.Outcome.Value);
        Assert.Empty(Artifacts(result, "i1"));
    }

    [Fact]
    public async Task An_artifact_from_an_older_head_stays_so_the_evidence_step_can_catch_it()
    {
        var stale = $$"""[{"kind":"app-run","path":"old.md","sha256":"sha256:aa","commit":"{{Old}}","at":"t","passing":true}]""";
        var files = new InMemoryArtifactFiles().With(Work, "new.md", "n");

        var result = await Run(files, Facts(manual: """{"artifacts":["new.md"]}""", proof: Proof(stale)));

        Assert.Equal(["old.md", "new.md"], Artifacts(result, "i1").Select(a => (string?)a!["path"]), StringComparer.Ordinal);
        Assert.Equal(Old, (string?)Artifacts(result, "i1")[0]!["commit"]);
    }

    [Fact]
    public async Task Collecting_again_does_not_add_the_same_file_on_the_same_head_twice()
    {
        var files = new InMemoryArtifactFiles().With(Work, "a.md", "a");
        var first = await Run(files, Facts(manual: """{"artifacts":["a.md"]}"""));
        var proof = first.Result!.Outputs[FactKey.From("fact:proof")].GetRawText();

        var again = await Run(files, Facts(manual: """{"artifacts":["a.md"]}""", proof: proof));

        _ = Assert.Single(Artifacts(again, "i1"));
    }

    [Fact]
    public async Task A_missing_proof_or_workspace_fails_the_step()
    {
        var facts = Facts(manual: """{"artifacts":["a.md"]}""");
        _ = facts.Remove("workspace");

        var result = await Run(new InMemoryArtifactFiles(), facts);

        Assert.NotNull(result.Failure);
        Assert.Contains("step.prove.collect", result.Failure, StringComparison.Ordinal);
    }
}
