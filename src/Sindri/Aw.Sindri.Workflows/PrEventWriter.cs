using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Appends one 'pr.changed' (PR babysitter spec §6.1) as the 'pr-watch' service: 'fact:prEvent' ('{ subject, headSha, changes[], at }') is stored first, then the
/// event goes to the ledger as an item event, with the outcome and the stored fact in the payload every registered event carries
/// (<see cref="RoutedItem"/> has that shape).
/// </summary>
/// <param name="ledger">The ledger, fenced by the run's lease.</param>
/// <param name="events">The control plane's event factory.</param>
/// <param name="facts">Stores 'fact:prEvent'.</param>
/// <param name="clock">Stamps 'at'.</param>
internal sealed class PrEventWriter(ILedgerStore ledger, EventFactory events, IFactStore facts, TimeProvider clock)
{
    /// <summary>The producer 'pr.changed' names.</summary>
    public const string Actor = "service:pr-watch";

    private const string Kind = "pr.changed";

    /// <summary>Appends <paramref name="change"/>.</summary>
    /// <param name="run">The scheduled run whose lease fences the append.</param>
    /// <param name="change">The event.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Done, or the error that stopped it (the poller then keeps the old digest, so the change is found again).</returns>
    public async ValueTask<Result<Unit>> Append(ScheduledRun run, PrChange change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(change);
        var stored = await facts.Put(EventFact(change), cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var payload = new RoutedItem(change.Outcome, new Dictionary<string, FactValue>(StringComparer.Ordinal) { ["fact:prEvent"] = stored.Value });
        var entry = events.Entry(Kind, payload, change.Key, change.Pull.Item, new EventScope(Actor, events.Scope.Tenant, events.Scope.Ring));
        var appended = await ledger.Append(entry, run.Grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }

    private byte[] EventFact(PrChange change)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("subject", change.Pull.Link);
            json.WriteString("headSha", change.HeadSha);
            json.WriteStartArray("changes");
            foreach (var name in change.Changes)
            {
                json.WriteStringValue(name);
            }

            json.WriteEndArray();
            json.WriteString("at", clock.GetUtcNow());
            json.WriteEndObject();
        }

        return buffer.ToArray();
    }
}
