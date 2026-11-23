using Aw.Kernel;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>
/// Reads what the doctor's lease, ledger and views checks judge: the lease, the ledger's schema and head, the views store's schema and the projector's state. Every read
/// goes through the store guard, so a file an older build wrote is a fail row naming it, never a crash (first-run spec §3.4, #2954).
/// </summary>
internal sealed class LedgerFacts(ILedgerStore ledger, IViewReader views, IDoctorStores stores)
{
    private const string LedgerFile = "ledger.db";
    private const string ViewsFile = "views.db";

    public ValueTask<Result<SchemaStatus>> Schema(CancellationToken cancellationToken) => stores.Guard(LedgerFile, ledger.Schema, cancellationToken);

    public ValueTask<Result<LeaseRow?>> Lease(ILease lease, CancellationToken cancellationToken) => stores.Guard(LedgerFile, lease.Read, cancellationToken);

    public async ValueTask<(Result<SchemaStatus> Schema, Result<ProjectorState?> State, Result<long> Head)> Views(CancellationToken cancellationToken)
    {
        var schema = await stores.Guard(ViewsFile, stores.ViewsSchema, cancellationToken).ConfigureAwait(false);
        var state = await stores.Guard(ViewsFile, views.State, cancellationToken).ConfigureAwait(false);
        var head = await stores.Guard(LedgerFile, ledger.Head, cancellationToken).ConfigureAwait(false);
        return (schema, state, head);
    }
}
