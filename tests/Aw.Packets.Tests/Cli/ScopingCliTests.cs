using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Cli;

/// <summary>The scoping kind through the CLI: --kind, validate, hash and freeze, and that only freeze writes (under packets/).</summary>
[Lane(TestLane.Integration)]
public sealed class ScopingCliTests : IDisposable
{
    private static readonly string[] _rules = ["PKT-SCP-001", "PKT-SCP-002", "PKT-SCP-003", "PKT-SCP-004", "PKT-SCP-005"];

    private readonly CliWorkspace _work = new();

    public void Dispose() => _work.Dispose();

    [Fact]
    public void Validate_passes_the_gold_scope_packet_names_every_rule_and_writes_nothing()
    {
        var packet = _work.WriteScopingPacket(A.ScopingPacket());

        var (exit, output, error) = _work.Run("validate", "--kind", "scoping", packet);

        Assert.Equal((0, ""), (exit, error));
        Assert.StartsWith("aw-packets validate: scoping packet — PASS (0 failed, 0 unchecked)\n", output, StringComparison.Ordinal);
        Assert.All(_rules, rule => Assert.Contains("pass      " + rule + " ", output, StringComparison.Ordinal));
        Assert.Empty(_work.StateFiles());
    }

    [Fact]
    public void Validate_a_broken_scope_packet_exits_1_and_json_has_one_record_per_failure()
    {
        var packet = _work.WriteScopingPacket(A.ScopingPacket().Without("brief.md").EditLenses("L-code-2", "L-product-2"));

        var (exit, output, _) = _work.Run("validate", packet, "--json", "--kind", "scoping");

        Assert.Equal(1, exit);
        using var report = JsonDocument.Parse(output);
        Assert.Equal("scoping", report.RootElement.GetProperty("kind").GetString());
        Assert.False(report.RootElement.GetProperty("passed").GetBoolean());
        var failed = report.RootElement.GetProperty("checks").EnumerateArray()
            .Where(static c => c.GetProperty("state").GetString() == "fail").Select(static c => c.GetProperty("rule").GetString()).ToList();
        Assert.Equal(["PKT-SCP-001", "PKT-SCP-005"], failed);
    }

    [Fact]
    public void Hash_prints_the_hash_alone_and_json_adds_the_kind_digest_version_and_files()
    {
        var packet = _work.WriteScopingPacket(A.ScopingPacket());

        var (exit, output, _) = _work.Run("hash", "--kind", "scoping", packet);
        var (jsonExit, jsonOutput, _) = _work.Run("hash", "--kind", "scoping", packet, "--json");

        Assert.Equal((0, 0), (exit, jsonExit));
        Assert.Matches("^sha256:[0-9a-f]{64}\n$", output);
        using var json = JsonDocument.Parse(jsonOutput);
        Assert.Equal(output.Trim(), json.RootElement.GetProperty("hash").GetString());
        Assert.Equal(("scoping", 1, 1), (json.RootElement.GetProperty("kind").GetString(), json.RootElement.GetProperty("schemaVersion").GetInt32(), json.RootElement.GetProperty("digestVersion").GetInt32()));
        Assert.Equal(14, json.RootElement.GetProperty("files").GetArrayLength());
        Assert.Empty(_work.StateFiles());
    }

    [Fact]
    public void Hash_of_a_build_packet_is_the_hash_freeze_records()
    {
        var packet = _work.WritePacket(A.Packet());
        _ = _work.Git("add", "-A");
        _ = _work.Git("-c", "user.name=t", "-c", "user.email=t@example.invalid", "commit", "-q", "-m", "packet");

        var (exit, output, _) = _work.Run("hash", packet);
        var (_, frozen, _) = _work.Run("freeze", packet, "--repo", _work.Repo);

        Assert.Equal(0, exit);
        Assert.Contains(output.Trim(), frozen, StringComparison.Ordinal);
    }

    [Fact]
    public void Freeze_records_the_hash_under_the_instance_once_and_hash_agrees()
    {
        var packet = _work.WriteScopingPacket(A.ScopingPacket());
        var hash = _work.Run("hash", "--kind", "scoping", packet).Out.Trim();

        var (firstExit, firstOut, _) = _work.Run("freeze", "--kind", "scoping", packet, "--repo-slug", "example/repo", "--instance", "idea-1");
        var (secondExit, secondOut, _) = _work.Run("freeze", "--kind", "scoping", packet, "--repo-slug", "example/repo", "--instance", "idea-1", "--json");

        Assert.Equal(0, firstExit);
        Assert.Equal($"aw-packets freeze: scoping packet idea-1 — frozen {hash} (packets/example/repo/idea-1/frozen/{hash["sha256:".Length..]}.json)\n", firstOut);
        Assert.Equal(0, secondExit);
        using var json = JsonDocument.Parse(secondOut);
        Assert.True(json.RootElement.GetProperty("alreadyFrozen").GetBoolean());
        var record = Assert.Single(_work.StateFiles());
        using var stored = JsonDocument.Parse(File.ReadAllText(Path.Combine(_work.State, record)));
        Assert.Equal("scoping", stored.RootElement.GetProperty("kind").GetString());
        Assert.Equal(hash, stored.RootElement.GetProperty("hash").GetString());
        Assert.False(stored.RootElement.TryGetProperty("baseSha", out _));
    }

    [Fact]
    public void Freeze_a_failing_scope_packet_exits_1_and_writes_nothing()
    {
        var packet = _work.WriteScopingPacket(A.ScopingPacket().With("extra.txt", "x"));

        var (exit, output, _) = _work.Run("freeze", "--kind", "scoping", packet, "--repo-slug", "example/repo", "--instance", "idea-1");

        Assert.Equal(1, exit);
        Assert.StartsWith("aw-packets freeze: scoping packet idea-1 — not frozen: FAIL (1 failed, 0 unchecked)", output, StringComparison.Ordinal);
        Assert.Empty(_work.StateFiles());
    }

    [Theory]
    [InlineData("validate", "--kind", "other", "x")]
    [InlineData("validate", "--kind")]
    [InlineData("validate", "--kind", "scoping", "x", "--repo", "y")]
    [InlineData("hash", "--kind", "scoping")]
    [InlineData("hash", "x", "--repo", "y")]
    [InlineData("freeze", "--kind", "scoping", "x")]
    [InlineData("freeze", "--kind", "scoping", "x", "--repo-slug", "example/repo")]
    public void A_usage_error_exits_2(params string[] args) =>
        Assert.Equal(2, _work.Run(args).Exit);

    [Theory]
    [InlineData("nowhere", 2, "AW-PACKETS-002")]
    [InlineData("example", 1, "AW-PACKETS-001")]
    public void Errors_go_to_stderr_with_their_code_and_exit_code(string what, int expectedExit, string code)
    {
        var packet = _work.WriteScopingPacket(A.ScopingPacket());
        var slug = what == "example" ? "not-a-slug" : "example/repo";
        var directory = what == "nowhere" ? Path.Combine(_work.Root, "nowhere") : packet;

        var (exit, output, error) = _work.Run("freeze", "--kind", "scoping", directory, "--repo-slug", slug, "--instance", "idea-1");

        Assert.Equal((expectedExit, ""), (exit, output));
        Assert.StartsWith($"aw-packets: {code} ", error, StringComparison.Ordinal);
    }
}
