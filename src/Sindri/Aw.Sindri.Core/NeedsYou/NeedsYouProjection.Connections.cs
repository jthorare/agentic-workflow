using Aw.Kernel;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.NeedsYou;

internal sealed partial class NeedsYouProjection
{
    // 'connection.needs-sign-in' puts the row 'connection:<name>'; 'connection.signed-in' removes it (connections PR 10b).
    private ValueTask<Result<Unit>> ConnectionRow(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken) => throw new NotSupportedException();
}
