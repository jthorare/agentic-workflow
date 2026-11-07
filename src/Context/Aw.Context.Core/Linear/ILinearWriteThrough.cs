using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>Stores one Linear issue's state right after a write changed it (Linear hub spec 2026-11-04 D4), so the Linear writes need not know how: the port of the write-through slice.</summary>
public interface ILinearWriteThrough
{
    /// <summary>Stores the issue's current state.</summary>
    /// <param name="scope">The caller's scope; its settings name the Linear source.</param>
    /// <param name="issueId">The issue's Linear id.</param>
    /// <param name="written">The assignee and state fields the write itself sent.</param>
    /// <param name="cancellationToken">Cancels the store.</param>
    /// <returns>True when the issue is in the store; false when it could not be stored (a purge was running, the re-read or the commit failed) or Linear no longer has it in one of the profile's teams.</returns>
    public ValueTask<bool> Store(ContextScope scope, ExternalId issueId, LinearWrittenFields written, CancellationToken cancellationToken);
}
