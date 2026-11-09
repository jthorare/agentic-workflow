using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>Reads one group: finds its connection and the poller for the connection's preset, then reads once or forward (<see cref="CatchUp"/>).</summary>
/// <param name="connections">The connections.</param>
/// <param name="pollers">One poller per preset.</param>
/// <param name="read">Reads one group, once or forward.</param>
internal sealed class GroupReader(IConnectionStore connections, IEnumerable<IEventPoller> pollers, CatchUp read)
{
    // A null value: the group can't be polled here (no usable connection or poller), which is not an error.
    internal async ValueTask<Result<ReadOutcome?>> Read(GroupRead request, LeaseGrant grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var found = await connections.Get(request.Group.Connection, cancellationToken).ConfigureAwait(false);
        if (found.IsError || found.Value.Status != ConnectionStatus.Connected || found.Value.Preset is not { } preset)
        {
            return Results.Success<ReadOutcome?>(value: null);
        }

        var poller = pollers.FirstOrDefault(p => string.Equals(p.Preset, preset, StringComparison.Ordinal));
        if (poller is null)
        {
            return Results.Success<ReadOutcome?>(value: null);
        }

        var done = await read.Read(new ReadRequest(request.Group, found.Value, poller, request.Cursor, request.Pages, request.CatchUp), grant, cancellationToken).ConfigureAwait(false);
        return done.IsError ? done.Error : Results.Success<ReadOutcome?>(done.Value);
    }
}
