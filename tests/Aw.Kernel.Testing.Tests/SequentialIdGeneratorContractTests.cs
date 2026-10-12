using Aw.Kernel.Ids;

namespace Aw.Kernel.Testing.Tests;

/// <summary>
/// The id generator contract bound to the fake. It is deterministic by design: two fresh generators in one scope
/// issue the same ids, so a test can predict them (IdAt). Real adapters keep the default and must not repeat.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class SequentialIdGeneratorContractTests : IdGeneratorContract
{
    protected override bool Deterministic => true;

    protected override IIdGenerator Create() => new SequentialIdGenerator();
}
