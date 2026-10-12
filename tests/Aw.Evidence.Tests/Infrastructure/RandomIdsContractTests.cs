using Aw.Evidence.Infrastructure.Ids;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using IdGeneratorContract = Aw.Evidence.Testing.IdGeneratorContract;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Unit)]
public sealed class RandomIdsContractTests : IdGeneratorContract
{
    protected override IIdGenerator Create() => new RandomIds();
}
