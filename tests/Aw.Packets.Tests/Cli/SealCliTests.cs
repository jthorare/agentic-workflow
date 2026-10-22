using Aw.Kernel.Testing;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Cli;

/// <summary>LN-PKT4 through the CLI: 'validate --kind phase --seal &lt;file&gt;'.</summary>
[Lane(TestLane.Integration)]
public sealed class SealCliTests : IDisposable
{
    private readonly CliWorkspace _work = new();

    public void Dispose() => _work.Dispose();

    private string WriteSeal(string setHash, string name = "seal.json")
    {
        var path = Path.Combine(_work.Root, name);
        File.WriteAllText(path, $"{{ \"schemaVersion\": 1, \"phase\": \"p1\", \"setHash\": \"{setHash}\", \"planHash\": \"{LinePacketBuilder.OtherHash}\" }}\n");
        return path;
    }

    [Fact]
    public void Validate_without_a_seal_leaves_PKT_HOLD_003_unchecked()
    {
        var packet = _work.WriteLinePacket(A.LinePacket(PacketKinds.Phase));

        var (exit, output, _) = _work.Run("validate", "--kind", "phase", packet, "--repo", _work.Repo);

        Assert.Equal(0, exit);
        Assert.Contains("unchecked PKT-HOLD-003 packet:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_matching_seal_passes_and_a_different_one_fails_with_exit_1()
    {
        var packet = _work.WriteLinePacket(A.LinePacket(PacketKinds.Phase));

        var (okExit, okOutput, _) = _work.Run("validate", "--kind", "phase", packet, "--repo", _work.Repo, "--seal", WriteSeal(LinePacketBuilder.Hash));
        var (badExit, badOutput, _) = _work.Run("validate", "--kind", "phase", packet, "--repo", _work.Repo, "--seal", WriteSeal(LinePacketBuilder.OtherHash, "other.json"));

        Assert.Equal((0, 1), (okExit, badExit));
        Assert.Contains("pass      PKT-HOLD-003", okOutput, StringComparison.Ordinal);
        Assert.Contains("fail      PKT-HOLD-003", badOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_or_bad_seal_file_is_AW_PACKETS_010_and_exit_2()
    {
        var packet = _work.WriteLinePacket(A.LinePacket(PacketKinds.Phase));
        File.WriteAllText(Path.Combine(_work.Root, "bad.json"), "{}");

        var (missingExit, _, missingError) = _work.Run("validate", "--kind", "phase", packet, "--repo", _work.Repo, "--seal", Path.Combine(_work.Root, "nowhere.json"));
        var (badExit, _, badError) = _work.Run("validate", "--kind", "phase", packet, "--repo", _work.Repo, "--seal", Path.Combine(_work.Root, "bad.json"));

        Assert.Equal((2, 2), (missingExit, badExit));
        Assert.StartsWith("aw-packets: AW-PACKETS-010 ", missingError, StringComparison.Ordinal);
        Assert.StartsWith("aw-packets: AW-PACKETS-010 ", badError, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_a_phase_takes_a_seal()
    {
        var packet = _work.WriteLinePacket(A.LinePacket(PacketKinds.Design));

        var (exit, _, error) = _work.Run("validate", "--kind", "design", packet, "--repo", _work.Repo, "--seal", WriteSeal(LinePacketBuilder.Hash));

        Assert.Equal(2, exit);
        Assert.StartsWith("usage: aw-packets ", error, StringComparison.Ordinal);
    }
}
