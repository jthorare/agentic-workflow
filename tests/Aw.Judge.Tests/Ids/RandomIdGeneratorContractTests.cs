using Aw.Judge.Infrastructure.Ids;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Ids;

/// <summary>The Kernel id generator contract bound to the judge's real adapter (random version-4 GUIDs).</summary>
[Lane(TestLane.Unit)]
public sealed class RandomIdGeneratorContractTests : IdGeneratorContract
{
    protected override IIdGenerator Create() => new RandomIdGenerator();
}
