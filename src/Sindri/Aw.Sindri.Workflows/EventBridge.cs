using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The ledger-to-Temporal bridge (engine spec rev 8 §5.4; engine P4 unit (d)): the built-in service 'workflow-events'
/// (S1-D8), on its own loop (S1-D10). Each run reads the ledger past its cursor, in order, and turns every registered
/// event whose envelope actor is its descriptor's producer into an 'event' signal: a 'route: item' event to every live
/// instance of its item (Temporal buffers it until the instance reaches the wait, so an early event isn't lost), a
/// 'route: causation' event only to the instance whose command caused it. Any other event, or one from another actor,
/// signals nothing. The cursor moves past an event once every target has it; an instance that has ended meanwhile is
/// skipped, and a Temporal outage or timeout, or a ledger failure, ends the run with the cursor before that event, so the next run retries it;
/// an event that can't be delivered for good (its item isn't an item key, Temporal refuses the query) is logged and passed.
/// An item event also stays in the held window (<see cref="HeldItemEvents"/>) for the visibility lag: each run ends by
/// re-sending it to an instance that started before it and that visibility didn't list yet when it was first delivered.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="catalog">The registered events.</param>
/// <param name="items">The live instances of an item (a visibility query on 'Item') and the held window.</param>
/// <param name="origins">Which instance a command was written for.</param>
/// <param name="runtime">Signals an instance.</param>
/// <param name="cursor">The last delivered sequence.</param>
/// <param name="logger">The log a refused or unroutable event is noted in.</param>
internal sealed partial class EventBridge(
    ILedgerStore ledger,
    BlockCatalog catalog,
    HeldItemEvents items,
    ICommandOrigins origins,
    IInstanceRuntime runtime,
    EventBridgeCursor cursor,
    ILogger<EventBridge> logger) : IScheduledService
{
    /// <summary>The actor of the triggers it delivers.</summary>
    public const string Actor = "service:workflow.events";

    private const int Batch = 100;

    private int _signalled;

    /// <summary>The service's name in the schedule ('workflow-events'; a service name is kebab-case).</summary>
    public static readonly ServiceName ServiceId = ServiceName.From("workflow-events");

    /// <summary>The bridge's place in the schedule without a profile entry: every 5 s, the shortest cadence.</summary>
    public static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(Cadence.Min));

    public ServiceName Identity() => ServiceId;

    /// <summary>Delivers the events after the cursor, then the held ones owed; <see cref="ScheduledResult.Quiet"/> when it signalled no instance (C8).</summary>
    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        _signalled = 0;
        var from = await cursor.Read(cancellationToken).ConfigureAwait(false);
        if (from.IsError)
        {
            return from.Error;
        }

        for (var at = from.Value; ;)
        {
            var read = await ledger.ReadAfter(at, Batch, cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            if (read.Value.Count == 0)
            {
                var redelivered = await Redeliver(cancellationToken).ConfigureAwait(false);
                return redelivered.IsError ? redelivered.Error : _signalled > 0 ? ScheduledResult.Worked : ScheduledResult.Quiet;
            }

            var delivered = await DeliverBatch(read.Value, cancellationToken).ConfigureAwait(false);
            if (delivered is { } failure)
            {
                return failure;
            }

            at = read.Value[^1].Sequence;
        }
    }

    /// <summary>
    /// Delivers a batch in order and moves the cursor once, past the last event handled. An event whose failure may pass
    /// (Temporal unavailable, a timeout) stops the batch with the cursor before it; one that can't (an invalid query) is
    /// logged and passed, so it can't stall every event behind it.
    /// </summary>
    private async ValueTask<Error?> DeliverBatch(IReadOnlyList<LedgerEvent> batch, CancellationToken cancellationToken)
    {
        long last = 0;
        Error? failure = null;
        foreach (var recorded in batch)
        {
            var delivered = await Deliver(recorded, cancellationToken).ConfigureAwait(false);
            if (delivered.IsError && delivered.Error.Kind is ErrorKind.Unavailable or ErrorKind.Timeout)
            {
                failure = delivered.Error;
                break;
            }

            if (delivered.IsError)
            {
                Refused(logger, recorded.Sequence, recorded.Envelope.Type, delivered.Error.Code, delivered.Error.Message);
            }

            last = recorded.Sequence;
        }

        if (last > 0)
        {
            var moved = await cursor.Advance(last, cancellationToken).ConfigureAwait(false);
            failure ??= moved.IsError ? moved.Error : null;
        }

        return failure;
    }

    /// <summary>Signals <paramref name="recorded"/> to every instance it routes to; nothing when it isn't a registered event from its producer.</summary>
    private async ValueTask<Result<Unit>> Deliver(LedgerEvent recorded, CancellationToken cancellationToken)
    {
        var descriptor = catalog.Events.FirstOrDefault(d => string.Equals(d.Kind.Value, recorded.Envelope.Type, StringComparison.Ordinal));
        if (descriptor is null)
        {
            return Unit.Value;
        }

        if (!string.Equals(recorded.Envelope.Actor, descriptor.Producer, StringComparison.Ordinal))
        {
            NotFromProducer(logger, recorded.Sequence, recorded.Envelope.Type, recorded.Envelope.Actor, descriptor.Producer);
            return Unit.Value;
        }

        var targets = await Targets(descriptor, recorded, cancellationToken).ConfigureAwait(false);
        if (targets.IsError)
        {
            return targets.Error;
        }

        var (trigger, problem) = DeliveredEvents.Trigger(descriptor, recorded, Actor);
        if (trigger is null)
        {
            NoRoute(logger, recorded.Sequence, recorded.Envelope.Type, problem ?? "it makes no trigger");
            return Unit.Value;
        }

        var sent = await SendAll(targets.Value, trigger, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error;
        }

        if (descriptor.Route == EventRoute.Item && recorded.Item is { Length: > 0 } item && ItemKey.TryFrom(item, out _))
        {
            items.Hold(item, trigger, targets.Value);
        }

        return Unit.Value;
    }

    /// <summary>Re-sends each held item event to the live instances of its item that haven't had it (visibility lag, §5.4).</summary>
    private async ValueTask<Result<Unit>> Redeliver(CancellationToken cancellationToken)
    {
        var owed = await items.Owed(cancellationToken).ConfigureAwait(false);
        if (owed.IsError)
        {
            return owed.Error;
        }

        foreach (var (trigger, target) in owed.Value)
        {
            var signalled = await Send(target, trigger, cancellationToken).ConfigureAwait(false);
            if (signalled.IsError)
            {
                return signalled.Error;
            }

            items.Delivered(trigger, target);
        }

        return Unit.Value;
    }

    private async ValueTask<Result<Unit>> SendAll(IReadOnlyList<InstanceAddress> targets, WaitSatisfied trigger, CancellationToken cancellationToken)
    {
        foreach (var target in targets)
        {
            var signalled = await Send(target, trigger, cancellationToken).ConfigureAwait(false);
            if (signalled.IsError)
            {
                return signalled.Error;
            }
        }

        return Unit.Value;
    }

    /// <summary>Signals one instance; one that has ended meanwhile is skipped.</summary>
    private async ValueTask<Result<Unit>> Send(InstanceAddress target, WaitSatisfied trigger, CancellationToken cancellationToken)
    {
        var signalled = await runtime.Signal(target, trigger, cancellationToken).ConfigureAwait(false);
        if (!signalled.IsError)
        {
            _signalled++;
        }

        return signalled.IsError && signalled.Error.Code != WorkflowErrors.InstanceNotFound.Code ? signalled.Error : Unit.Value;
    }

    /// <summary>The instances <paramref name="recorded"/> routes to (spec §5.4): its item's live instances, or its command's instance.</summary>
    private async ValueTask<Result<IReadOnlyList<InstanceAddress>>> Targets(EventDescriptor descriptor, LedgerEvent recorded, CancellationToken cancellationToken)
    {
        if (descriptor.Route is EventRoute.Causation or EventRoute.Parent)
        {
            var causer = await origins.CauserOf(recorded, cancellationToken).ConfigureAwait(false);
            if (causer.IsError)
            {
                return causer.Error;
            }

            if (causer.Value is { } address)
            {
                return new[] { descriptor.Route == EventRoute.Parent ? address.LinkedParent() ?? address : address };   // LN-WF1: a linked instance's event goes to its line
            }

            NoRoute(logger, recorded.Sequence, recorded.Envelope.Type, "its causation names no instance's command");
            return Array.Empty<InstanceAddress>();
        }

        if (recorded.Item is not { Length: > 0 } item)
        {
            NoRoute(logger, recorded.Sequence, recorded.Envelope.Type, "it names no item");
            return Array.Empty<InstanceAddress>();
        }

        if (!ItemKey.TryFrom(item, out _))
        {
            NoRoute(logger, recorded.Sequence, recorded.Envelope.Type, "its item isn't an item key");
            return Array.Empty<InstanceAddress>();
        }

        return await items.LiveOf(item, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "event {Sequence} ({Type}) was written by {Actor}, not its producer {Producer}; nothing was signalled")]
    private static partial void NotFromProducer(ILogger logger, long sequence, string type, string actor, string producer);

    [LoggerMessage(Level = LogLevel.Warning, Message = "event {Sequence} ({Type}) can't be delivered and is passed over: {Code} {Why}")]
    private static partial void Refused(ILogger logger, long sequence, string type, string code, string why);

    [LoggerMessage(Level = LogLevel.Information, Message = "event {Sequence} ({Type}) routes to no instance: {Why}")]
    private static partial void NoRoute(ILogger logger, long sequence, string type, string why);
}
