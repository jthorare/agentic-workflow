using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Testing;

/// <summary>The lease and the ledger over one store: the ledger fences every append against that store's lease.</summary>
/// <param name="Lease">The lease.</param>
/// <param name="Ledger">The ledger.</param>
public sealed record SindriStores(ILease Lease, ILedgerStore Ledger);
