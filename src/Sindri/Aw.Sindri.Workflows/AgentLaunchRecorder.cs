using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Records an engine launch's outcome (engine P8 D12): 'launch.started' or 'launch.failed' under the ONE key 'launch.outcome:&lt;launch&gt;' the launcher service uses, so the first
/// outcome written wins and a retry of the activity records nothing twice. A started launch starts its heartbeat clock, so a session that dies before its first beat is still ended as lost.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="lease">The lease this process holds; every append is fenced by it.</param>
/// <param name="events">Builds the entries.</param>
/// <param name="liveness">The heartbeats the launcher service watches.</param>
/// <param name="clock">The time the clock starts at.</param>
internal sealed class AgentLaunchRecorder(ILedgerStore ledger, HeldLease lease, EventFactory events, LaunchLiveness liveness, TimeProvider clock)
{
    /// <summary>Records the session <paramref name="started"/> as the outcome of <paramref name="request"/>.</summary>
    /// <param name="request">The launch.</param>
    /// <param name="started">What the launcher started.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Done; throws when the ledger refuses, so the activity retries.</returns>
    public async Task Started(LaunchRequest request, StartedLaunch started, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(started);
        var chosen = started.ChosenSession is { } id && SessionId.TryFrom(id, out var session) ? session : (SessionId?)null;
        var viewer = new LaunchViewer("none", Label: null, started.Viewer.Kind, started.Viewer.Code, started.Viewer.Detail);
        var payload = new LaunchStarted(request.Launch, request.Item, request.Mode, request.Agent, chosen, "sindri " + request.Item.Value, viewer, started.Attach, started.CatalogSource, started.ArgvHash, started.Sandbox);
        var appended = await Append(events.Entry(EventTypes.LaunchStarted, payload, Key(request.Launch), request.Item.Value), cancellationToken).ConfigureAwait(false);
        if (!appended.AlreadyRecorded)
        {
            liveness.Beat(request.Launch, clock.GetUtcNow());
        }
    }

    /// <summary>Records the launch as failed.</summary>
    /// <param name="request">The launch.</param>
    /// <param name="failed">What the launcher said.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Done; throws when the ledger refuses, so the activity retries.</returns>
    public async Task Failed(LaunchRequest request, FailedLaunch failed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(failed);
        var payload = new LaunchFailed(request.Launch, request.Item, failed.Error.Code, failed.Error.Message, failed.Stage, SandboxRefusal.ReasonOf(failed.Error), SandboxRefusal.SourceTextOf(failed.Error));
        _ = await Append(events.Entry(EventTypes.LaunchFailed, payload, Key(request.Launch), request.Item.Value), cancellationToken).ConfigureAwait(false);
    }

    private async Task<LedgerAppend> Append(LedgerEntry entry, CancellationToken cancellationToken)
    {
        var grant = lease.Current ?? throw new InvalidOperationException("SND-LEASE-002: this process holds no lease; the launch outcome waits for one");
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? throw new InvalidOperationException($"{appended.Error.Code}: {appended.Error.Message}") : appended.Value;
    }

    // The launcher service's key (LaunchOutcomes): one per launch, shared by started and failed.
    private static string Key(LaunchId launch) => "launch.outcome:" + launch.Value;
}
