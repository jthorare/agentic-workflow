using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.NeedsYou;

internal sealed partial class NeedsYouProjection
{
    // 'connection.needs-sign-in' puts the row 'connection:<name>'; 'connection.signed-in' removes it (connections PR 10b). A second needs-sign-in keeps the first row and its 'since'.
    private async ValueTask<Result<Unit>> ConnectionRow(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        if (recorded.Envelope.Type == EventTypes.ConnectionSignedIn)
        {
            var signed = events.Payload<ConnectionSignedIn>(recorded);
            return signed.IsError ? signed.Error : await rows.Delete(ViewNames.NeedsYou, "connection:" + signed.Value.Connection, cancellationToken).ConfigureAwait(false);
        }

        var needs = events.Payload<ConnectionNeedsSignIn>(recorded);
        if (needs.IsError)
        {
            return needs.Error;
        }

        var key = "connection:" + needs.Value.Connection;
        var existing = await rows.Get<NeedsYouRowRecord>(ViewNames.NeedsYou, key, cancellationToken).ConfigureAwait(false);
        if (existing.IsError || existing.Value is not null)
        {
            return existing.IsError ? existing.Error : Unit.Value;
        }

        var name = ConnectionName.Parse(needs.Value.Connection);
        if (name.IsError)
        {
            return name.Error;
        }

        return await Put(rows, NeedsYouRows.ForConnection(name.Value, needs.Value.Reason, needs.Value.At, scrubber), recorded, cancellationToken).ConfigureAwait(false);
    }
}
