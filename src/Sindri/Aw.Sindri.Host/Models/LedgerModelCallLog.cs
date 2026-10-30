using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// Writes the router's 'context.model.called' event to the ledger as 'service:context' (model routing spec §4 "Writing events"), the way SyncContextSourcesService writes its events: through
/// <see cref="ILedgerStore"/> with the held lease. Best effort: a lost lease, a ledger error or a command run without the control plane is logged and never reaches the model call.
/// </summary>
/// <param name="held">The lease the event is fenced by.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="logger">Where a failed append is noted.</param>
internal sealed partial class LedgerModelCallLog(HeldLease held, ILedgerStore ledger, EventFactory events, ILogger<LedgerModelCallLog> logger) : IModelCallLog
{
    private const string Actor = "service:context";

    public async ValueTask Record(ContextModelCalled called, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(called);
        if (held.Current is not { } grant)
        {
            LogNotWritten(logger, called.CallId.Value, "the control plane doesn't hold the lease");
            return;
        }

        try
        {
            var entry = events.Entry(EventTypes.ContextModelCalled, called, $"{EventTypes.ContextModelCalled}:{called.CallId.Value}", item: null, new EventScope(Actor, events.Scope.Tenant, events.Scope.Ring));
            var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
            if (appended.IsError)
            {
                LogNotWritten(logger, called.CallId.Value, appended.Error.Code);
            }
        }
#pragma warning disable CA1031 // best effort: whatever the append throws is logged, never raised into the model call
        catch (Exception e) when (e is not OperationCanceledException)
#pragma warning restore CA1031
        {
            LogNotWritten(logger, called.CallId.Value, e.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "context.model.called {Call} was not written: {Reason}")]
    private static partial void LogNotWritten(ILogger logger, string call, string reason);
}
