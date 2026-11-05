using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.ModelCheck;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.ModelCheck;
using Aw.Sindri.Features.Ship;
using Aw.Sindri.Infrastructure.ModelCheck;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Prove;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.BuildShip;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.ModelCheck;

/// <summary>Follow-ups to the model check (#2095): the move check matches a moved model by its path under specs/tla/, and a skipped check counts the spec folders it found so the hand-off can say a repo has none.</summary>
[Lane(TestLane.Unit)]
public sealed class ModelCheckFollowupTests
{
    private const string Address = "T-1/line/1/phase.p1.1/ship.1";
    private const string Tree = "/work/p1";
    private const string Checkout = "/checkouts/name";
    private const string PacketDir = "/packets/demo-p1";
    private const string ProofDir = "/runs/proof-pack";
    private const string ReviewDir = "/runs/review-pack";
    private const string BaseSha = "0123456789abcdef0123456789abcdef01234567";
    private const string SetText = """{ "schemaVersion": 1, "phase": "demo-p1", "scenarios": [ { "id": "H1", "item": "AC-1", "kind": "api", "steps": ["open an expired link"], "expected": "status 410" }, { "id": "C1", "item": null, "kind": "canary", "steps": [], "expected": "never", "phrase": "violet otter ledger 4417" } ] }""";

    private static readonly ModelCheckSetup _setup = new("/home/u/tlc-check.sh", "/usr/bin:/bin", "/home/u", "/home/u/.agentic-workflow", "/home/u/tlc.sh", "2g", Path.Combine(Path.GetTempPath(), "aw-model-check-followup-tests"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_scoping_model_whose_name_matches_a_spec_folder_outside_specs_tla_was_not_moved()
    {
        var rig = new StepRig();
        var specs = new FakeSpecFolders().Add(Folder("docs/models/Foo"));
        var packets = new InMemoryPacketDirectory();
        _ = packets.Put("/packets/p1", new Dictionary<string, string>(StringComparer.Ordinal) { ["model/Foo/Foo.tla"] = "x" });
        _ = rig.Commands.Answer("model-check", CheckRun.Of(0, timedOut: false, TimeSpan.FromSeconds(1), "tlc-check: Foo/Foo.cfg: PASS distinct=5 depth=3"));
        var step = new ModelCheckStep(specs, rig.Changes, rig.Runner, new PassThroughScrubber(), _setup, packets, new DiskScratchDirectories());

        var result = await step.Run(ModelContext("/packets/p1"), Token);

        Assert.Equal("specError", result.Result!.Outcome.Value);
        var finding = StepRig.Out(result, "findings").AsArray().Single()!;
        Assert.Equal("MDL-SPEC-003", (string?)finding["rule"]);
        Assert.Contains("The scoping model Foo was not moved", (string?)finding["message"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_skipped_check_with_no_spec_folders_records_a_count_of_zero()
    {
        var rig = new StepRig();
        var step = new ModelCheckStep(new FakeSpecFolders(), rig.Changes, rig.Runner, new PassThroughScrubber(), _setup, new InMemoryPacketDirectory(), new DiskScratchDirectories());

        var result = await step.Run(ModelContext(activePacket: null), Token);

        Assert.Equal("skipped", result.Result!.Outcome.Value);
        Assert.Equal(0, (int?)StepRig.Out(result, "modelCheck")["specFolders"]);
    }

    [Theory]
    [InlineData(""","specFolders":0""", "Model check skipped: this repo has no TLA+ specs (specs/tla/)")]
    [InlineData(""","specFolders":2""", "Model check skipped: no modelled code changed")]
    [InlineData("", "Model check skipped: no modelled code changed")]
    public async Task The_hand_off_names_a_repo_with_no_specs_when_the_check_was_skipped(string extra, string line)
    {
        var rig = new PackRig();
        var check = """{"skipped":true,"looked":[],"specs":[]""" + extra + "}";

        var result = await rig.Step().Run(PackContext(check), Token);

        Assert.Null(result.Failure);
        Assert.Contains(line, rig.Draft()["body.md"], StringComparison.Ordinal);
    }

    private static SpecFolder Folder(string path) => new(path,
    [
        new SpecFile("Foo.tla", SpecFileKind.Regular, "---- MODULE Foo ----\nEXTENDS Naturals, TLC\n===="),
        new SpecFile("Foo.cfg", SpecFileKind.Regular, "SPECIFICATION Spec\nINVARIANT Inv\n"),
        new SpecFile("README.md", SpecFileKind.Regular, "models: src/Timers.cs\n# Foo\nBounds: small.\n"),
    ]);

    private static StepContext ModelContext(string? activePacket)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["change"] = $$"""{"repo":"{{StepRig.Tree}}","base":"b0","head":"h1"}""",
            ["workspace"] = $"\"{StepRig.Tree}\"",
        };
        if (activePacket is not null)
        {
            facts["activePacket"] = $$"""{"hash":"sha256:x","dir":"{{activePacket}}"}""";
        }

        return StepRig.Context("step.model.check", facts) with
        {
            Arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["scope"] = JsonSerializer.SerializeToElement("all"),
                ["witnesses"] = JsonSerializer.SerializeToElement(value: true),
            },
        };
    }

    private static StepContext PackContext(string modelCheck)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["phasePacket"] = $$"""{"hash":"sha256:phase","dir":"{{PacketDir}}","base":{"rule":"default-branch","ref":""},"stackParent":null}""",
            ["proofPack"] = $$"""{"hash":"sha256:proof","dir":"{{ProofDir}}","head":"h1"}""",
            ["review"] = $$"""{"hash":"sha256:review","dir":"{{ReviewDir}}","head":"h1"}""",
            ["lineContext"] = $$"""{"line":"demo","phase":"p1","attempt":1,"base":"{{BaseSha}}","runArea":"/runs"}""",
            ["workspace"] = Tree,
            ["green"] = """{"commands":["dotnet build"]}""",
            ["modelCheck"] = modelCheck,
        };
        return BuildShipContexts.Of("step.ship.pack", facts) with { Address = Address, Repo = RepoName.From("name") };
    }

    private sealed class PackRig
    {
        private readonly InMemoryPacketDirectory _files = new();
        private readonly InMemoryHoldoutStore _store = new();
        private readonly FakeRunDiff _diff = new();
        private readonly InMemoryRepoCheckouts _checkouts = new InMemoryRepoCheckouts().With("name", Checkout);

        public PackRig()
        {
            _ = _files.Put(PacketDir, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["packet.json"] = $$"""{"id":"demo-p1","repo":"owner/name","baseSha":"{{BaseSha}}"}""",
                ["phase.json"] = new JsonObject { ["goal"] = "Expired links answer 410", ["kind"] = "feature", ["base"] = JsonNode.Parse("""{"rule":"default-branch","ref":"main"}"""), ["testChange"] = JsonNode.Parse("[]") }.ToJsonString(),
                ["acceptance.json"] = """{"schemaVersion":1,"parent":"sha256:root","items":[]}""",
            });
            _ = _files.Put(ProofDir, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["acceptance.json"] = """{"schemaVersion":1,"parent":"sha256:aa","items":[{"id":"AC-1","text":"An expired link answers 410","status":"passing","evidence":[{"kind":"api","path":"evidence/api.json","sha256":"sha256:0123456789abcdef0123456789abcdef","commit":"abcdef1234567","at":"2026-10-24T10:00:00Z"}]}]}""",
                ["holdout.json"] = """{"schemaVersion":1,"commit":"h1","results":[{"scenario":"H1","item":"AC-1","verdict":"pass","spent":false}]}""",
                ["not-proven.md"] = "# Not proven\n",
                ["proof.json"] = """{"schemaVersion":1,"head":"h1","items":[],"notRun":[]}""",
            });
            _ = _files.Put(ReviewDir, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["review.json"] = """{"schemaVersion":1,"head":"h1","verdict":"clean","round":2,"counts":{"blocking":0,"issue":0,"suggestions":0,"nits":0},"rounds":[],"findings":[]}""",
            });
            _store.Put("T-1", "demo-p1", SetText);
            _diff.Add(Tree, "h1");
        }

        public ShipPackStep Step() => new(_files, _checkouts, _store, _diff, new PassThroughScrubber());

        public IReadOnlyDictionary<string, string> Draft() => _files.Read(InMemoryPacketDirectory.DirectoryOf(Address, "handoff"), Token).AsTask().Result.Value;
    }
}
