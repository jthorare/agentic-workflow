using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Setup;

/// <summary>Records 'context.pin.written' (concept pins spec §3.6): the pins a write touched, the profile hash it made and who wrote them, fenced by the lease this process holds.</summary>
/// <param name="held">The lease the event is fenced by.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller who wrote the pins.</param>
public sealed class PinEvents(HeldLease held, ILedgerStore ledger, EventFactory events, ICallerContextAccessor callers)
{
    /// <summary>Whether an event can be recorded now: this process holds the lease and a call is running. A job checks it before it changes anything.</summary>
    public bool CanRecord() => held.Current is not null && callers.Current is not null;

    /// <summary>The idempotency key of a write: one per profile hash it made.</summary>
    /// <param name="hash">The profile hash after the write.</param>
    /// <returns>'context.pin.written:&lt;hash&gt;'.</returns>
    public static string KeyOf(ProfileHash hash) => "context.pin.written:" + hash.Value;

    /// <summary>Appends 'context.pin.written' under <see cref="KeyOf"/>; recording it again is a no-op.</summary>
    /// <param name="hash">The profile hash after the write.</param>
    /// <param name="pins">Each pin touched as '&lt;id&gt;:&lt;action&gt;'.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Success, or SND-API-001 when this process holds no lease or no call is running, or the ledger's error.</returns>
    public async ValueTask<Result<Unit>> Written(ProfileHash hash, IReadOnlyList<string> pins, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pins);
        if (held.Current is not { } grant || callers.Current is not { } caller)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var appended = await ledger.Append(events.Entry(EventTypes.ContextPinWritten, new ContextPinWritten(caller.CommandId, caller.Actor, caller.Launch, hash, pins), KeyOf(hash)), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }
}
