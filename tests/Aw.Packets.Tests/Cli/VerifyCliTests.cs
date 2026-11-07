using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Cli;

/// <summary>LN-PKT2 through the CLI: 'verify &lt;dir&gt; --hash &lt;h&gt; [--candidate]' against the records 'freeze' writes.</summary>
[Lane(TestLane.Integration)]
public sealed class VerifyCliTests : IDisposable
{
    private readonly CliWorkspace _work = new();

    public void Dispose() => _work.Dispose();

    [Fact]
    public void Verify_of_a_frozen_build_packet_exits_0_and_says_who_froze_it_and_writes_nothing_new()
    {
        var packet = _work.WritePacket(A.Packet());
        var hash = _work.Run("hash", packet).Out.Trim();
        _ = _work.Run("freeze", packet, "--by", "owner", "--repo", _work.Repo);
        var before = _work.StateFiles();

        var (exit, output, error) = _work.Run("verify", packet, "--hash", hash);

        Assert.Equal((0, ""), (exit, error));
        Assert.StartsWith($"aw-packets verify: packet demo-packet — verified {hash}, frozen by owner at ", output, StringComparison.Ordinal);
        Assert.Contains($"(packets/example/repo/demo-packet/frozen/{hash["sha256:".Length..]}.json)", output, StringComparison.Ordinal);
        Assert.Equal(before, _work.StateFiles());
    }

    [Fact]
    public void Json_is_one_versioned_document_with_by()
    {
        var packet = _work.WritePacket(A.Packet());
        var hash = _work.Run("hash", packet).Out.Trim();
        _ = _work.Run("freeze", packet, "--repo", _work.Repo);

        var (exit, output, _) = _work.Run("verify", "--json", packet, "--hash", hash);

        Assert.Equal(0, exit);
        using var json = JsonDocument.Parse(output);
        Assert.Equal((1, "build", true, true, "machine", hash), (
            json.RootElement.GetProperty("schemaVersion").GetInt32(),
            json.RootElement.GetProperty("kind").GetString(),
            json.RootElement.GetProperty("verified").GetBoolean(),
            json.RootElement.GetProperty("frozen").GetBoolean(),
            json.RootElement.GetProperty("by").GetString(),
            json.RootElement.GetProperty("computed").GetString()));
    }

    [Fact]
    public void An_edited_packet_is_a_mismatch_exit_1_naming_both_hashes()
    {
        var packet = _work.WritePacket(A.Packet());
        var frozen = _work.Run("hash", packet).Out.Trim();
        _ = _work.Run("freeze", packet, "--repo", _work.Repo);
        File.AppendAllText(Path.Combine(packet, "out-of-scope.json"), "\n");
        File.WriteAllText(Path.Combine(packet, "out-of-scope.json"), File.ReadAllText(Path.Combine(packet, "out-of-scope.json")).Replace("anything else", "anything other", StringComparison.Ordinal));
        var now = _work.Run("hash", packet).Out.Trim();

        var (exit, output, _) = _work.Run("verify", packet, "--hash", frozen);

        Assert.Equal(1, exit);
        Assert.Contains($"MISMATCH: the packet hashes to {now}, not {frozen}", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_hash_that_was_never_frozen_is_not_frozen_exit_1_and_a_candidate_matches_without_a_record()
    {
        var packet = _work.WritePacket(A.Packet());
        var hash = _work.Run("hash", packet).Out.Trim();

        var (exit, output, _) = _work.Run("verify", packet, "--hash", hash);
        var (candidateExit, candidateOutput, _) = _work.Run("verify", packet, "--hash", hash, "--candidate");

        Assert.Equal(1, exit);
        Assert.Contains("NOT FROZEN: no freeze record for " + hash, output, StringComparison.Ordinal);
        Assert.Equal(0, candidateExit);
        Assert.Contains("hash matches", candidateOutput, StringComparison.Ordinal);
        Assert.Contains("candidate", candidateOutput, StringComparison.Ordinal);
        Assert.Empty(_work.StateFiles());
    }

    [Theory]
    [InlineData("design")]
    [InlineData("plan")]
    [InlineData("phase")]
    [InlineData("proof")]
    [InlineData("review")]
    [InlineData("handoff")]
    public void Every_line_kind_verifies_after_freeze(string kind)
    {
        var packet = _work.WriteLinePacket(A.LinePacket(PacketKinds.Find(kind)!));
        var hash = _work.Run("hash", "--kind", kind, packet).Out.Trim();
        _ = _work.Run("freeze", "--kind", kind, packet, "--by", "owner", "--repo", _work.Repo);

        var (exit, output, _) = _work.Run("verify", "--kind", kind, packet, "--hash", hash);

        Assert.Equal(0, exit);
        Assert.Contains($"verified {hash}, frozen by owner", output, StringComparison.Ordinal);
        Assert.StartsWith($"aw-packets verify: {kind} packet ", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_scope_packet_is_verified_under_its_repo_slug_and_instance()
    {
        var packet = _work.WriteScopingPacket(A.ScopingPacket());
        var hash = _work.Run("hash", "--kind", "scoping", packet).Out.Trim();
        _ = _work.Run("freeze", "--kind", "scoping", packet, "--repo-slug", "example/repo", "--instance", "idea-1", "--by", "owner");

        var (exit, output, _) = _work.Run("verify", "--kind", "scoping", packet, "--hash", hash, "--repo-slug", "example/repo", "--instance", "idea-1");
        var (otherExit, _, _) = _work.Run("verify", "--kind", "scoping", packet, "--hash", hash, "--repo-slug", "example/repo", "--instance", "idea-2");

        Assert.Equal(0, exit);
        Assert.Contains("frozen by owner", output, StringComparison.Ordinal);
        Assert.Equal(1, otherExit);
    }

    [Theory]
    [InlineData("verify", "x")]
    [InlineData("verify", "--hash", "sha256:00")]
    [InlineData("verify", "x", "--hash", "nonsense")]
    [InlineData("verify", "x", "--hash", "sha256:0000000000000000000000000000000000000000000000000000000000000000", "--hash", "sha256:0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("verify", "x", "--hash", "sha256:0000000000000000000000000000000000000000000000000000000000000000", "--by", "owner")]
    [InlineData("verify", "x", "--kind", "scoping", "--hash", "sha256:0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("verify", "x", "--hash", "sha256:0000000000000000000000000000000000000000000000000000000000000000", "--candidate", "--candidate")]
    public void A_usage_error_exits_2(params string[] args)
    {
        var (exit, _, error) = _work.Run(args);

        Assert.Equal(2, exit);
        Assert.StartsWith("usage: aw-packets ", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_malformed_freeze_record_is_AW_PACKETS_008_exit_2()
    {
        var packet = _work.WritePacket(A.Packet());
        var hash = _work.Run("hash", packet).Out.Trim();
        _ = _work.Run("freeze", packet, "--repo", _work.Repo);
        var record = Path.Combine(_work.State, Assert.Single(_work.StateFiles()));
        File.WriteAllText(record, File.ReadAllText(record).Replace("\"machine\"", "\"robot\"", StringComparison.Ordinal));

        var (exit, _, error) = _work.Run("verify", packet, "--hash", hash);

        Assert.Equal(2, exit);
        Assert.StartsWith("aw-packets: AW-PACKETS-008 ", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_packet_is_AW_PACKETS_002_exit_2()
    {
        var (exit, _, error) = _work.Run("verify", Path.Combine(_work.Root, "nowhere"), "--hash", "sha256:" + new string('0', 64));

        Assert.Equal(2, exit);
        Assert.StartsWith("aw-packets: AW-PACKETS-002 ", error, StringComparison.Ordinal);
    }
}
