using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Infrastructure.Packets;
using Aw.Sindri.Testing.Packets;

namespace Aw.Sindri.Tests.Line;

/// <summary>The on-disk freeze records honour the freeze-record contract, laid out as aw-packets freeze writes them.</summary>
[Lane(TestLane.Integration)]
public sealed class FrozenPacketRecordsContractTests : FrozenPacketsContract, IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("aw-sindri-frozen-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    protected override IFrozenPackets Create(IReadOnlyList<FrozenPacket> held)
    {
        foreach (var record in held)
        {
            var directory = Path.Combine(_root, "fixture-labs", "fixture-notes", record.Packet, "frozen");
            _ = Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, record.Hash[7..] + ".json"),
                $"{{\"schemaVersion\":1,\"repo\":\"fixture-labs/fixture-notes\",\"packet\":\"{record.Packet}\",\"hash\":\"{record.Hash}\",\"digestVersion\":1,\"kind\":\"{record.Kind}\",\"by\":\"{record.By.ToString().ToLowerInvariant()}\",\"frozenAt\":\"2026-10-16T11:05:00Z\"}}");
        }

        return new FrozenPacketRecords(_root);
    }

    [Fact]
    public async Task A_record_from_before_by_existed_reads_as_a_machine_freeze()
    {
        var hash = "sha256:" + new string('c', 64);
        var directory = Path.Combine(_root, "o", "n", "old-packet", "frozen");
        _ = Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, new string('c', 64) + ".json"), $"{{\"schemaVersion\":1,\"repo\":\"o/n\",\"packet\":\"old-packet\",\"hash\":\"{hash}\",\"digestVersion\":1,\"frozenAt\":\"2026-10-16T11:05:00Z\"}}", TestContext.Current.CancellationToken);

        var found = await new FrozenPacketRecords(_root).Find(hash, TestContext.Current.CancellationToken);

        Assert.Equal(new FrozenPacket(hash, "old-packet", "build", PacketFreezer.Machine), found.Value);
    }

    [Fact]
    public async Task A_record_whose_own_hash_differs_from_its_file_name_is_not_a_freeze_of_that_hash()
    {
        var directory = Path.Combine(_root, "o", "n", "forged", "frozen");
        _ = Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, new string('d', 64) + ".json"), $"{{\"schemaVersion\":1,\"repo\":\"o/n\",\"packet\":\"forged\",\"hash\":\"sha256:{new string('e', 64)}\",\"digestVersion\":1,\"kind\":\"design\",\"frozenAt\":\"2026-10-16T11:05:00Z\"}}", TestContext.Current.CancellationToken);

        var found = await new FrozenPacketRecords(_root).Find("sha256:" + new string('d', 64), TestContext.Current.CancellationToken);

        Assert.True(found.IsSuccess);
        Assert.Null(found.Value);
    }

    [Fact]
    public async Task A_missing_packets_directory_holds_no_freeze()
    {
        var found = await new FrozenPacketRecords(Path.Combine(_root, "nowhere")).Find("sha256:" + new string('a', 64), TestContext.Current.CancellationToken);

        Assert.True(found.IsSuccess);
        Assert.Null(found.Value);
    }
}
