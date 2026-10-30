using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.PinConcepts;

namespace Aw.Sindri.Tests.PinConcepts;

/// <summary>The PinConcepts job (concept pins spec §3.6, "The write"): it writes pins into 'index.concepts', returns the new hash and never approves.</summary>
[Lane(TestLane.Unit)]
public sealed class PinConceptsServiceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pin_concepts_writes_the_entry_and_returns_the_new_hash_without_approving()
    {
        var done = await new PinConceptsService().Handle(new PinConceptsRequest(Set: [new PinSet("Billing", Summary: "How we take money", Aliases: ["payments"])]), Token);

        Assert.True(done.IsSuccess);
        Assert.Equal([new PinWritten(ConceptPinId.From("billing"), "Billing", "created")], done.Value.Pins);
        Assert.False(done.Value.Approved);
        Assert.Matches("^[0-9a-f]{64}$", done.Value.Hash.Value);
    }
}
