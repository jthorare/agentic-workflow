using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.NeedsYou;

namespace Aw.Sindri.Core.Lanes;

/// <summary>The lane rows of Home's needs-you list ('lane-recycle', 'agent-limited', 'pr-ready-merge'): what is open now, and the raise and clear that change it through the ledger.</summary>
public interface ILaneRows
{
    /// <summary>The ids of the open rows of the three lane kinds.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The row ids ('lane-recycle:api').</returns>
    public ValueTask<Result<IReadOnlyList<NeedsYouRowId>>> Open(CancellationToken cancellationToken);

    /// <summary>Opens the row 'kind:key'. A row that is already open keeps its first 'since'.</summary>
    /// <param name="kind">One of the three lane kinds.</param>
    /// <param name="key">The lane, subject or PR the row is about.</param>
    /// <param name="title">The headline.</param>
    /// <param name="reason">Why it needs the owner.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or the ledger's error.</returns>
    public ValueTask<Result<Unit>> Raise(NeedsYouKind kind, string key, string title, string reason, CancellationToken cancellationToken);

    /// <summary>Clears the open row with this id.</summary>
    /// <param name="id">The row id.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or the ledger's error.</returns>
    public ValueTask<Result<Unit>> Clear(NeedsYouRowId id, CancellationToken cancellationToken);
}
