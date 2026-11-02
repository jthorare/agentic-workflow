using System.Diagnostics;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Infrastructure.Packets;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Packets;

/// <summary>
/// The proof pack <see cref="ProofPack"/> assembles, against the real built aw-packets (built by the solution build): the pack passes the proof kind's checks
/// and freezes, which the in-memory fake cannot show. The repo is the scoping fixture's history, whose commit the gold packets name as their base.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ProofPackRealBinaryTests : IDisposable
{
    private const string Head = "abcabcabcabcabcabcabcabcabcabcabcabcabca";
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ProofFacts Facts()
    {
        var acceptance = JsonNode.Parse("""{"schemaVersion":1,"parent":"sha256:9b274b16f381c1ae0aa3a4988b4d5e04710db9afa0432304050254fa6e63c57b","items":[{"id":"AC-1","text":"one","status":"failing","evidence":[]},{"id":"AC-2","text":"two","status":"failing","evidence":[]}]}""")!;
        var signals = JsonNode.Parse("""{"schemaVersion":1,"signals":[{"item":"AC-1","id":"SG-1a","kind":"test","statement":"s","runs":"head"},{"item":"AC-2","id":"SG-2a","kind":"test","statement":"t","runs":"head"}]}""")!;
        var proof = JsonNode.Parse($$"""{"head":"{{Head}}","items":[{"id":"AC-1","artifacts":[{"kind":"test","path":"evidence/a.txt","sha256":"sha256:{{new string('a', 64)}}","commit":"{{Head}}","at":"2026-10-19T10:00:00Z","passing":true,"signal":"SG-1a"}]},{"id":"AC-2","artifacts":[]}]}""");
        return new ProofFacts(
            Head,
            acceptance,
            signals,
            "feature",
            HoldoutCount: 1,
            proof,
            Red: JsonNode.Parse("""{"AC-1":["T1"],"AC-2":["T2"]}"""),
            BeforeAfter: JsonNode.Parse($$"""{"commit":"{{Head}}","results":[]}"""),
            Holdout: JsonNode.Parse($$"""{"commit":"{{Head}}","canary":"clean","results":[{"scenario":"H1","item":"AC-1","verdict":"pass"}]}"""));
    }

    [Fact]
    public async Task The_assembled_pack_passes_the_proof_checks_and_freezes_by_machine()
    {
        var facts = Facts();
        var acceptanceText = facts.Acceptance.ToJsonString();
        var origin = new ProofPackOrigin("line-expiry-p1-proof", "fixture-labs/fixture-notes", BaseSha(), "sha256:" + new string('b', 64), ProofPack.Sha256(acceptanceText));
        var dir = Path.Combine(_home.Path, "proof-pack");
        _ = Directory.CreateDirectory(dir);
        foreach (var (name, text) in ProofPack.Files(facts, ProofChecks.Run(facts), origin))
        {
            await File.WriteAllTextAsync(Path.Combine(dir, name), text, Token);
        }

        var packet = new PacketRef(PacketKind.Proof, dir, Repo: FixtureRepo());
        var tool = Tool();
        var validation = await tool.Validate(packet, Token);
        var freeze = await tool.Freeze(packet, PacketFreezer.Machine, Token);

        Assert.True(validation.Value.Passed, string.Join("; ", validation.Value.Failures));
        Assert.True(freeze.Value.Frozen, string.Join("; ", freeze.Value.Failures));
        Assert.Matches("^sha256:[0-9a-f]{64}$", freeze.Value.Hash);
    }

    private static string BaseSha() => (string)JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "tests", "fixtures", "line", "gold", "design", "expiry", "packet.json")))!["baseSha"]!;   // why: the fixture packet names one

    private AwPacketsTool Tool()
    {
        var wrapper = Path.Combine(_home.Path, "aw-packets");
        _ = ExecutableScript.Write(wrapper, $"AW_STATE_DIR='{Path.Combine(_home.Path, "state")}'\nexport AW_STATE_DIR\nexec dotnet '{BuiltDll()}' \"$@\"\n");

        return new AwPacketsTool(new ProcessToolRunner(), name => string.Equals(name, "AW_PACKETS_BIN", StringComparison.Ordinal) ? wrapper : null);
    }

    private string FixtureRepo()
    {
        var parent = Path.Combine(_home.Path, "history");
        var start = new ProcessStartInfo("bash") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(RepoRoot(), "tests", "fixtures", "scoping", "history", "build.sh"));
        start.ArgumentList.Add(parent);
        using var process = Process.Start(start)!;   // why: the program is bash, which starts
        var output = process.StandardOutput.ReadToEndAsync(Token);
        var error = process.StandardError.ReadToEndAsync(Token);
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, output.Result + error.Result);
        return Path.Combine(parent, "fixture-notes");
    }

    private static string BuiltDll()
    {
        var bin = Path.Combine(RepoRoot(), "artifacts", "bin", "Aw.Packets.App");
        var dlls = Directory.Exists(bin) ? Directory.GetFiles(bin, "aw-packets.dll", SearchOption.AllDirectories) : [];
        Assert.True(dlls.Length > 0, "aw-packets is not built: dotnet build src/Packets/Aw.Packets.App");
        return dlls.OrderByDescending(File.GetLastWriteTimeUtc).First();
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("AgenticWorkflow.slnx not found above the test assembly");
    }
}
