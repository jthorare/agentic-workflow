using Aw.Kernel.Testing;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Infrastructure.Packets;
using Aw.Sindri.Testing.Line;

namespace Aw.Sindri.Tests.Line;

/// <summary>Until C14's ingest exists, the pending adapter honours the contract by saying why it stored nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PendingOwnerDecisionsContractTests : OwnerDecisionsContract
{
    protected override IOwnerDecisions Create() => new PendingOwnerDecisions();

    [Fact]
    public async Task It_names_the_missing_ingest_in_its_reason()
    {
        var result = await Create().Ingest(Core.Workflow.Engine.InstanceAddress.From("T-3/plan/1"), "sha256:" + new string('a', 64), [new OwnerDecision("text", [], "A1")], TestContext.Current.CancellationToken);

        Assert.Contains("C14", result.Value.Skipped, StringComparison.Ordinal);
    }
}
