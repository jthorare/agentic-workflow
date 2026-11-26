using Aw.Judge.Core.Ports;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.CheckTraits;

[Lane(TestLane.Unit)]
public sealed class InMemoryTraitVerdictCacheContractTests : TraitVerdictCacheContract
{
    protected override ITraitVerdictCache Create(TimeProvider clock, TimeSpan keep, int maxEntries) => new InMemoryTraitVerdictCache(clock, keep, maxEntries);

    protected override ITraitVerdictCache CreateUnavailable() => new InMemoryTraitVerdictCache { Unavailable = true };
}
