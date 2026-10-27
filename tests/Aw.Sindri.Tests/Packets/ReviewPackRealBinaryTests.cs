using System.Diagnostics;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Review;
using Aw.Sindri.Infrastructure.Packets;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Packets;

/// <summary>
/// The review result packet <see cref="ReviewPack"/> assembles, against the real built aw-packets (built by the solution build): the packet passes the review kind's checks
/// and freezes by machine, which the in-memory fake cannot show. The repo is the scoping fixture's history, whose commit the gold packets name as their base.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ReviewPackRealBinaryTests : IDisposable
{
    private const string Head = "abcabcabcabcabcabcabcabcabcabcabcabcabca";
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ReviewFacts Facts(string verdict) => new(
        Head,
        JsonNode.Parse($$"""{"round":2,"verdict":"{{verdict}}","blocking":{{(verdict == "clean" ? "0" : "1")}},"issue":0,"suggestions":1,"nits":0,"findings":[{"lens":"correctness","severity":"suggestion","text":"rename","resolution":"kept"}]}""")!,
        JsonNode.Parse("""[{"round":1,"lens":"correctness","findings":[{"id":"f1","severity":"blocking","text":"b"}],"closes":true},{"round":2,"lens":"correctness","findings":[{"severity":"suggestion","text":"rename"}],"closes":true}]"""));

    private async Task<string> Write(ReviewFacts facts, string name)
    {
        var origin = new ReviewPackOrigin("line-expiry-p1-review", "fixture-labs/fixture-notes", BaseSha(), "sha256:" + new string('b', 64), ProofPack.Sha256("""{"schemaVersion":1}"""));
        var dir = Path.Combine(_home.Path, name);
        _ = Directory.CreateDirectory(dir);
        foreach (var (file, text) in ReviewPack.Files(facts, origin))
        {
            await File.WriteAllTextAsync(Path.Combine(dir, file), text, Token);
        }

        return dir;
    }

    [Theory]
    [InlineData("clean")]
    [InlineData("behaviourWrong")]
    public async Task The_assembled_packet_passes_the_review_checks_and_freezes_by_machine(string verdict)
    {
        var dir = await Write(Facts(verdict), "review-pack");
        var packet = new PacketRef(PacketKind.Review, dir, Repo: FixtureRepo());
        var tool = Tool();

        var validation = await tool.Validate(packet, Token);
        var freeze = await tool.Freeze(packet, PacketFreezer.Machine, Token);

        Assert.True(validation.Value.Passed, string.Join("; ", validation.Value.Failures));
        Assert.True(freeze.Value.Frozen, string.Join("; ", freeze.Value.Failures));
        Assert.Equal(PacketFreezer.Machine, freeze.Value.By);
        Assert.Matches("^sha256:[0-9a-f]{64}$", freeze.Value.Hash);
    }

    [Fact]
    public async Task A_packet_with_a_file_the_kind_does_not_list_fails_with_a_PKT_REV_code_and_freezes_nothing()
    {
        var dir = await Write(Facts("clean"), "review-extra");
        await File.WriteAllTextAsync(Path.Combine(dir, "notes.md"), "stray", Token);
        var packet = new PacketRef(PacketKind.Review, dir, Repo: FixtureRepo());
        var tool = Tool();

        var freeze = await tool.Freeze(packet, PacketFreezer.Machine, Token);

        Assert.False(freeze.Value.Frozen);
        Assert.Contains(freeze.Value.Failures, static f => f.Rule.StartsWith("PKT-REV-", StringComparison.Ordinal));
        Assert.Null(freeze.Value.Hash);
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
