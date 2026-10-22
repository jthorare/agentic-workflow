using Aw.Kernel.Testing;
using Aw.Packets.Core;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class InMemorySealSourceContractTests : SealSourceContract
{
    protected override ISealSource Create(string path, byte[] bytes) => new InMemorySealSource().With(path, bytes);
}
