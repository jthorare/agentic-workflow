using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.Sessions;

/// <summary>
/// Appends 'fleet.changed' (collector spec CO3). Called by the collector host with the lease grant; it has no command gate, audit row or catalog row (the
/// CheckProfileService pattern). The payload holds no text: no cwd, title or socket.
/// </summary>
internal sealed class RecordFleetChangesService(ILedgerStore ledger, EventFactory events, CollectorMemory memory) : IService<RecordFleetChangesRequest, RecordFleetChangesResponse>
{
    public async ValueTask<Result<RecordFleetChangesResponse>> Handle(RecordFleetChangesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var recorded = 0;
        var done = 0;
        foreach (var change in request.Changes)
        {
            var key = string.Create(CultureInfo.InvariantCulture, $"{EventTypes.FleetChanged}:{change.Ref.Provider.Value}:{change.Ref.Id.Value}:{change.Kind.ToString().ToLowerInvariant()}:{change.ObservedAt.ToUnixTimeMilliseconds()}");
            var payload = new FleetChanged(change.Ref.Provider, change.Ref.Id, change.Kind, change.State, change.Reason, change.Repo, change.HostKind, change.LaunchId);
            var appended = await ledger.Append(events.Entry(EventTypes.FleetChanged, payload, key), request.Grant, cancellationToken).ConfigureAwait(false);
            if (appended.IsError)
            {
                memory.Pending = [.. memory.Pending.Skip(done)];   // the ones before this one are in the ledger; this one and the rest wait for the next pass
                return appended.Error;
            }

            done++;
            recorded += appended.Value.AlreadyRecorded ? 0 : 1;
        }

        memory.Pending = [.. memory.Pending.Skip(done)];   // the loop is the only writer, so what it passed in is the head of the queue
        return new RecordFleetChangesResponse(recorded);
    }

}
