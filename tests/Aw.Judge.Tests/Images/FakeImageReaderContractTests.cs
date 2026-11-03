using Aw.Judge.Core.Ports;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Images;

[Lane(TestLane.Unit)]
public sealed class FakeImageReaderContractTests : ImageReaderContract
{
    protected override (IImageReader Reader, string Directory) Create(IReadOnlyDictionary<string, byte[]> files) =>
        (new FakeImageReader(files.ToDictionary(f => "/fake/" + f.Key, f => f.Value, StringComparer.Ordinal)), "/fake");
}
