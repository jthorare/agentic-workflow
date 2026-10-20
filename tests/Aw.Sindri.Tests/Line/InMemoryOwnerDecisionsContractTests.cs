using Aw.Kernel.Testing;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Testing.Line;

namespace Aw.Sindri.Tests.Line;

/// <summary>The in-memory owner decisions honour the contract.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryOwnerDecisionsContractTests : OwnerDecisionsContract
{
    protected override IOwnerDecisions Create() => new InMemoryOwnerDecisions();
}
