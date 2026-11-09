using Aw.Kernel.Testing;
using Aw.Sindri.Testing.Handoff;

namespace Aw.Sindri.Tests.Handoff;

/// <summary>The card's proof: the fake and the HTTP client agree on list, claim and submit.</summary>
[Lane(TestLane.Integration)]
public sealed class InboxContractTests
{
    [Fact]
    public async Task The_fake_inbox_and_the_http_client_agree_on_list_claim_and_submit()
    {
        await InboxContract.ListClaimAndSubmit(InboxPairs.Fake());
        await InboxContract.ListClaimAndSubmit(InboxPairs.Http());
    }
}
