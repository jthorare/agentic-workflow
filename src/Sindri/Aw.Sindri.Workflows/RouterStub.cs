using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The router stub (workflow engine spec §12, engine P7 plan D8), a built-in service: for each 'workflow.item' instance that
/// started, it appends one 'item.routed' event as the scheduler records it (service:router-stub), whose outcome comes from the approved profile's
/// 'workflow.route' ('startable', or 'notStartable' for 'none') and whose 'fact:stepSpec' is the item's spec
/// (<see cref="ItemRouter"/>). The event bridge delivers it to the instance. It reads the ledger past its own cursor, and an
/// event is keyed by the 'workflow.started' row it answers, so a crash between the append and the cursor repeats nothing.
/// Real routing replaces it.
/// </summary>
/// <param name="ledger">The ledger it reads 'workflow.started' from and appends to (fenced by the lease of the scheduled run).</param>
/// <param name="events">The control plane's event factory.</param>
/// <param name="router">Decides the routing of one started instance.</param>
/// <param name="cursor">The last 'workflow.started' sequence it handled.</param>
/// <param name="logger">The log an item it can't route is noted in.</param>
internal sealed partial class RouterStub(ILedgerStore ledger, EventFactory events, ItemRouter router, RouterStubCursor cursor, ILogger<RouterStub> logger) : IScheduledService
{
    /// <summary>The producer the 'item.routed' descriptor names.</summary>
    public const string Actor = "service:router-stub";

    private const int Batch = 100;
    private const string ItemRouted = "item.routed";

    /// <summary>The service's name in the schedule ('router-stub').</summary>
    public static readonly ServiceName ServiceId = ServiceName.From("router-stub");

    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Routes each started item to its workflow.";

    /// <summary>The stub's place in the schedule without a profile entry: every 5 s.</summary>
    public static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(Cadence.Min), Description);

    /// <inheritdoc/>
    public ServiceName Identity() => ServiceId;

    /// <summary>Routes the 'workflow.item' instances that started since it last ran.</summary>
    /// <param name="run">The scheduled run.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>Worked when it routed an item, else quiet.</returns>
    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var from = await cursor.Read(cancellationToken).ConfigureAwait(false);
        if (from.IsError)
        {
            return from.Error;
        }

        var routed = 0;
        for (var at = from.Value; ;)
        {
            var read = await ledger.ReadAfter(at, Batch, cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            if (read.Value.Count == 0)
            {
                return routed > 0 ? ScheduledResult.Worked : ScheduledResult.Quiet;
            }

            var handled = await Handle(read.Value, run, cancellationToken).ConfigureAwait(false);
            routed += handled.Routed;
            if (handled.Failure is { } failure)
            {
                return failure;
            }

            at = read.Value[^1].Sequence;
        }
    }

    /// <summary>Handles a batch in order and moves the cursor once, past the last row handled; a row that fails stops the batch with the cursor before it.</summary>
    private async ValueTask<(int Routed, Error? Failure)> Handle(IReadOnlyList<LedgerEvent> batch, ScheduledRun run, CancellationToken cancellationToken)
    {
        long last = 0;
        var routed = 0;
        Error? failure = null;
        foreach (var row in batch)
        {
            var appended = await Route(row, run, cancellationToken).ConfigureAwait(false);
            if (appended.IsError)
            {
                failure = appended.Error;
                break;
            }

            routed += appended.Value ? 1 : 0;
            last = row.Sequence;
        }

        if (last > 0)
        {
            var moved = await cursor.Advance(last, cancellationToken).ConfigureAwait(false);
            failure ??= moved.IsError ? moved.Error : null;
        }

        return (routed, failure);
    }

    /// <summary>Appends 'item.routed' for <paramref name="row"/> when it is a started 'workflow.item'; true when it did.</summary>
    private async ValueTask<Result<bool>> Route(LedgerEvent row, ScheduledRun run, CancellationToken cancellationToken)
    {
        if (!string.Equals(row.Envelope.Type, EventTypes.WorkflowStarted, StringComparison.Ordinal) || AddressOf(row) is not { } address)
        {
            return false;
        }

        var decided = await router.Route(address, cancellationToken).ConfigureAwait(false);
        if (decided.IsError)
        {
            return decided.Error;
        }

        if (decided.Value is not { } routed)
        {
            return false;
        }

        var entry = events.Entry(ItemRouted, routed, string.Create(CultureInfo.InvariantCulture, $"router-stub:{row.Sequence}"), row.Item, new EventScope(Actor, row.Envelope.Tenant, row.Envelope.Ring));
        var appended = await ledger.Append(entry, run.Grant, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        Routed(logger, address, routed.Outcome);
        return true;
    }

    private static string? AddressOf(LedgerEvent row)
    {
        using var payload = JsonDocument.Parse(row.Envelope.Payload);
        return payload.RootElement.TryGetProperty("instance", out var instance) ? instance.GetString() : null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "routed {Address}: {Outcome}")]
    private static partial void Routed(ILogger logger, string address, string outcome);
}
