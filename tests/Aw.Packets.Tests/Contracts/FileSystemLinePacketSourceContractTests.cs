using Aw.Kernel.Testing;
using Aw.Packets.App.Infrastructure;
using Aw.Packets.Core;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Line;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Contracts;

[Lane(TestLane.Integration)]
public sealed class FileSystemLinePacketSourceContractTests : LinePacketSourceContract, IDisposable
{
    private readonly TempDirectory _root = new();

    protected override string Directory => Path.Combine(_root.Path, "pkt");

    protected override string MissingDirectory => Path.Combine(_root.Path, "missing");

    protected override ILinePacketSource Create(IReadOnlyDictionary<string, byte[]> files)
    {
        _ = _root.Write("pkt", files);
        return new FileSystemLinePacketSource();
    }

    [Fact]
    public async Task A_symbolic_link_for_a_listed_file_is_refused_not_followed()
    {
        var source = Create(A.LinePacket(PacketKinds.Review).Without("review.json").Build());
        var outside = Path.Combine(_root.Path, "secret.json");
        await File.WriteAllTextAsync(outside, "{}", TestContext.Current.CancellationToken);
        _ = File.CreateSymbolicLink(Path.Combine(Directory, "review.json"), outside);

        var read = await source.Read(PacketKinds.Review, Directory, CancellationToken.None);

        Assert.Equal(PacketsErrors.Malformed.Code, read.Error.Code);
        Assert.Contains("symbolic link", read.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_linked_directory_that_is_a_symbolic_link_is_refused()
    {
        var source = Create(A.LinePacket(PacketKinds.Proof).Build());
        var outside = Path.Combine(_root.Path, "outside");
        _ = System.IO.Directory.CreateDirectory(outside);
        System.IO.Directory.Delete(Path.Combine(Directory, "evidence"), recursive: true);
        _ = System.IO.Directory.CreateSymbolicLink(Path.Combine(Directory, "evidence"), outside);

        var read = await source.Read(PacketKinds.Proof, Directory, CancellationToken.None);

        Assert.Equal(PacketsErrors.Malformed.Code, read.Error.Code);
        Assert.Contains("symbolic link", read.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_symbolic_link_inside_a_linked_directory_is_refused()
    {
        var source = Create(A.LinePacket(PacketKinds.Proof).Build());
        var outside = Path.Combine(_root.Path, "secret.txt");
        await File.WriteAllTextAsync(outside, "x", TestContext.Current.CancellationToken);
        _ = File.CreateSymbolicLink(Path.Combine(Directory, "evidence", "link.txt"), outside);

        var read = await source.Read(PacketKinds.Proof, Directory, CancellationToken.None);

        Assert.Equal(PacketsErrors.Malformed.Code, read.Error.Code);
    }

    [Fact]
    public async Task More_files_than_the_limit_under_the_linked_directories_is_refused()
    {
        var files = A.LinePacket(PacketKinds.Proof).Build();
        for (var i = 0; i <= LineLimits.MaxLinkedFiles; i++)
        {
            files["evidence/f" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".txt"] = "x"u8.ToArray();
        }

        var read = await Create(files).Read(PacketKinds.Proof, Directory, CancellationToken.None);

        Assert.Equal(PacketsErrors.Malformed.Code, read.Error.Code);
    }

    [Fact]
    public async Task More_files_than_the_old_2000_limit_are_read()
    {
        var files = A.LinePacket(PacketKinds.Proof).Build();
        for (var i = 0; i <= 2_000; i++)
        {
            files["evidence/f" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".txt"] = "x"u8.ToArray();
        }

        var read = await Create(files).Read(PacketKinds.Proof, Directory, CancellationToken.None);

        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : null);
    }

    public void Dispose() => _root.Dispose();
}
