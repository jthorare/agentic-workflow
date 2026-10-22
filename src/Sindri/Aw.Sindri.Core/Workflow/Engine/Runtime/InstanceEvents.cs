using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// Which ledger events belong to an instance (engine spec §5.6, §6.3 'workflow events'; S1-D6): the interpreter
/// mirrors each under the key 'workflow:{workflow id}:{run id}:{activity id}', and the workflow id is the address, so
/// an instance's events are the ones keyed under its address. A child's address extends its parent's with '/', so the
/// ':' after the address keeps a parent's filter from taking its children's events.
/// </summary>
public static class InstanceEvents
{
    /// <summary>The empty state of 'workflow events'.</summary>
    public const string None = "no events";

    /// <summary>Whether <paramref name="recorded"/> was mirrored by <paramref name="address"/>.</summary>
    /// <param name="recorded">The ledger event.</param>
    /// <param name="address">The instance.</param>
    /// <returns>True when it is one of the instance's events.</returns>
    public static bool Belongs(LedgerEvent recorded, InstanceAddress address)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        return recorded.IdempotencyKey.StartsWith(LedgerFilter.InstanceKeyPrefix(address), StringComparison.Ordinal);
    }

    /// <summary>Whether <paramref name="recorded"/> was mirrored by the instance whose id is <paramref name="id"/>: the id is read from the ledger's own keys, so it needs no Temporal.</summary>
    /// <param name="recorded">The ledger event.</param>
    /// <param name="id">The instance's opaque id.</param>
    /// <returns>True when its key names an address that mints <paramref name="id"/>.</returns>
    public static bool Belongs(LedgerEvent recorded, WorkflowInstanceId id)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        const string Prefix = "workflow:";
        var key = recorded.IdempotencyKey;
        if (!key.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var end = key.IndexOf(':', Prefix.Length);
        return end > Prefix.Length && InstanceAddress.TryFrom(key[Prefix.Length..end], out var address) && WorkflowInstanceId.Of(address) == id;
    }
}
