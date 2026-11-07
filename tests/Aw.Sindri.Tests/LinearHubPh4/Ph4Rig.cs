using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Features.LinearIssues;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.LinearIssues;

namespace Aw.Sindri.Tests.LinearHubPh4;

/// <summary>The shared setup of the Linear hub Ph4 tests: the slice harness with the lease held, one stored issue ENG-12 and the owner's, a session's and a service's callers.</summary>
internal sealed class Ph4Rig
{
    internal const string Key = "ENG-12";

    internal Ph4Rig()
    {
        Facts = new FakeLinearIssueFacts()
            .WithSource(LinearIssueRows.Connected(LinearIssueRows.Viewer))
            .WithDetails(new LinearIssueDetailFacts(Issue, "Sync drops rows after a restart.", Removed: false, []));
    }

    internal SliceHarness H { get; } = new();

    internal FakeLinearIssueFacts Facts { get; }

    internal LinearIssueRowView Issue { get; } = LinearIssueRows.Row(Key, LinearIssueRows.Viewer);

    internal RecordKey Record => RecordKey.From(Issue.Record);

    internal static ItemAlias Alias => ItemAlias.From(Key);

    internal static CancellationToken Token => TestContext.Current.CancellationToken;

    internal async Task Hold() => _ = await H.HoldLease();

    internal IDisposable Owner() => H.Callers.Enter(A.Caller().Human().Build());

    internal IDisposable Session() => H.Callers.Enter(A.Caller().Session().Build());

    internal IDisposable Service() => H.Callers.Enter(A.Caller().Service("linear-pull").Build());

    internal IReadOnlyList<LedgerEvent> Events(string type) => [.. H.Store.Events.Where(e => string.Equals(e.Envelope.Type, type, StringComparison.Ordinal))];

    /// <summary>An item the owner made, with no link.</summary>
    internal async Task<ItemId> NewItem(string title = "Sync drops rows")
    {
        using var entered = Owner();
        var created = await new CreateItemService(H.Profiles, H.Held, H.Store, H.Events, H.Ids, H.Callers).Handle(new CreateItemRequest(title), Token);
        return created.Value.ItemId;
    }
}
