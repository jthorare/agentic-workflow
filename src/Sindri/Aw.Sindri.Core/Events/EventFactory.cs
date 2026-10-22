using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Events;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// Builds ledger entries: the caller's actor and correlation when a call is running (API spec C6), otherwise the scope's
/// actor ('host:sindri') and a fresh correlation; the scope's tenant and ring; the payload as AwJson. It never makes up a
/// 'service:' or 'human:' actor.
/// </summary>
/// <param name="scope">Who the process acts as when no caller is set.</param>
/// <param name="clock">The clock.</param>
/// <param name="ids">The id source.</param>
/// <param name="json">The package's AwJson options (the payload records are in its context).</param>
/// <param name="callers">The caller of the code running now.</param>
public sealed class EventFactory(EventScope scope, TimeProvider clock, IIdGenerator ids, JsonSerializerOptions json, ICallerContextAccessor callers)
{
    /// <summary>An entry of <paramref name="type"/> with <paramref name="payload"/> and the idempotency <paramref name="key"/>, about no item.</summary>
    /// <param name="type">The event type, for example 'process.started'.</param>
    /// <param name="payload">The payload record.</param>
    /// <param name="key">The idempotency key: what identifies the fact, so recording it twice is a no-op.</param>
    /// <typeparam name="TPayload">The payload type.</typeparam>
    /// <returns>The entry.</returns>
    public LedgerEntry Entry<TPayload>(string type, TPayload payload, string key) => Entry(type, payload, key, item: null);

    /// <summary>An entry of <paramref name="type"/> with <paramref name="payload"/> and the idempotency <paramref name="key"/>, about <paramref name="item"/>.</summary>
    /// <param name="type">The event type, for example 'item.created'.</param>
    /// <param name="payload">The payload record.</param>
    /// <param name="key">The idempotency key: what identifies the fact, so recording it twice is a no-op.</param>
    /// <param name="item">The work item the event concerns, or null.</param>
    /// <typeparam name="TPayload">The payload type.</typeparam>
    /// <returns>The entry.</returns>
    public LedgerEntry Entry<TPayload>(string type, TPayload payload, string key, string? item) => Entry(type, payload, key, item, Scope);

    /// <summary>The scope the process acts in when no caller is set: what the other overloads record.</summary>
    public EventScope Scope { get; } = scope;

    /// <summary>The id of the command in force.</summary>
    /// <returns>The id, or null outside a command.</returns>
    public CommandId? CurrentCommandId() => callers.Current?.CommandId;

    /// <summary>
    /// An entry of <paramref name="type"/> about <paramref name="item"/> as <paramref name="on"/> says (its actor, tenant and ring), for an event
    /// whose acting trigger and instance the recorder knows (a workflow row carries the human's or the timer's actor and the instance's tenant
    /// and ring). A caller set for the running call still wins for the actor, as in the other overloads.
    /// </summary>
    /// <param name="type">The event type.</param>
    /// <param name="payload">The payload record.</param>
    /// <param name="key">The idempotency key.</param>
    /// <param name="item">The work item the event concerns, or null.</param>
    /// <param name="on">The actor, tenant and ring to record.</param>
    /// <typeparam name="TPayload">The payload type.</typeparam>
    /// <returns>The entry.</returns>
    public LedgerEntry Entry<TPayload>(string type, TPayload payload, string key, string? item, EventScope on)
    {
        ArgumentNullException.ThrowIfNull(on);
        var caller = callers.Current;
        var context = EventContext.Create(caller?.Actor ?? on.Actor, on.Tenant, on.Ring, caller?.CorrelationId.Value ?? ids.NewId()).Value;
        var text = Encoding.UTF8.GetString(AwJson.Serialize(payload, json));
        return new LedgerEntry(context.Envelope(type, text, clock, ids), key, item);
    }

    /// <summary>Reads <paramref name="recorded"/>'s payload as <typeparamref name="TPayload"/>, through the same AwJson options.</summary>
    /// <param name="recorded">A recorded event.</param>
    /// <typeparam name="TPayload">The payload type its event type carries.</typeparam>
    /// <returns>The payload, or the JSON error when it doesn't read as one.</returns>
    public Result<TPayload> Payload<TPayload>(LedgerEvent recorded) => AwJson.Deserialize<TPayload>(Encoding.UTF8.GetBytes(recorded.Envelope.Payload), json);
}
