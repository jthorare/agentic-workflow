using Aw.Kernel.Testing;
using Aw.Packets.App.Infrastructure;
using Aw.Packets.Core;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Contracts;

[Lane(TestLane.Integration)]
public sealed class FileSystemSealSourceContractTests : SealSourceContract, IDisposable
{
    private readonly TempDirectory _root = new();

    protected override ISealSource Create(string path, byte[] bytes)
    {
        File.WriteAllBytes(Path.Combine(_root.Path, path), bytes);
        return new RootedSealSource(_root.Path, new FileSystemSealSource());
    }

    [Fact]
    public async Task A_directory_is_not_a_seal_file()
    {
        var source = Create("seal.json", []);
        _ = Directory.CreateDirectory(Path.Combine(_root.Path, "folder"));

        var read = await source.Read("folder", TestContext.Current.CancellationToken);

        Assert.Equal(PacketsErrors.SealUnusable.Code, read.Error.Code);
    }

    public void Dispose() => _root.Dispose();

    private sealed class RootedSealSource(string root, ISealSource inner) : ISealSource
    {
        public ValueTask<Kernel.Result<Core.Values.PacketHash>> Read(string path, CancellationToken cancellationToken) =>
            inner.Read(Path.Combine(root, path), cancellationToken);
    }
}
