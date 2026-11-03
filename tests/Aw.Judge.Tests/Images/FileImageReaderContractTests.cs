using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Images;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Images;

[Lane(TestLane.Integration)]
public sealed class FileImageReaderContractTests : ImageReaderContract, IDisposable
{
    private readonly ScratchDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    protected override (IImageReader Reader, string Directory) Create(IReadOnlyDictionary<string, byte[]> files)
    {
        foreach (var (name, bytes) in files)
        {
            _ = _directory.Write(name, bytes);
        }

        return (new FileImageReader(), _directory.Path);
    }
}
