using System.Globalization;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Skills;
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
        var viewer = LaunchViewer.Background(started.Viewer);
        var payload = new LaunchStarted(request.Launch, request.Item, request.Mode, request.Agent, chosen, "sindri " + request.Item.Value, viewer, started.Attach, started.CatalogSource, started.ArgvHash,
            request.Instance, request.Step?.Ref.State, request.Run?.Number, Sandbox: started.Sandbox, Model: request.Step?.Model, Skill: SkillOf(request, started))
        {
            PrHead = started.PrHead,
        };
        var appended = await Append(events.Entry(EventTypes.LaunchStarted, payload, Key(request.Launch), request.Item.Value), cancellationToken).ConfigureAwait(false);
        if (!appended.AlreadyRecorded)
        {
            liveness.Beat(request.Launch, clock.GetUtcNow());
        }
    }

    // A trial is always scrubbed, so it never pushes whatever the block asked for (skill flows spec §7).
    private static LaunchSkill? SkillOf(LaunchRequest request, StartedLaunch started)
    {
        if (request.Skill is not { } skill)
        {
            return null;
        }

        var trial = skill.Trial || skill.TrialSkillsDirectory is not null;
        return new LaunchSkill(skill.Skill, skill.DescriptorVersion, skill.ManifestPath, skill.SkillMdSha, Push: skill.Block.Pushes && !trial, trial, started.RunDirectory is { } run ? SkillLaunchEnv.ResultFileOf(run, request.Launch) : null);
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
        var payload = new LaunchFailed(request.Launch, request.Item, failed.Error.Code, failed.Error.Message, LaunchFailureDetail.Of(failed.Error, failed.Stage),
            request.Instance, request.Step?.Ref.State, request.Run?.Number);
        _ = await Append(events.Entry(EventTypes.LaunchFailed, payload, Key(request.Launch), request.Item.Value), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Records that the launch waits for a free session slot, once per launch (the first wait): 'launch.waiting:&lt;launch&gt;'. Other errors than a full cap record nothing.</summary>
    /// <param name="request">The launch.</param>
    /// <param name="failed">The launcher's SND-LAUNCH-020 answer, which names how many sessions run and the cap.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Done; throws when the ledger refuses, so the activity retries.</returns>
    public async Task Waiting(LaunchRequest request, FailedLaunch failed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(failed);
        var values = SandboxBlocker.Of(failed.Error).Values;
        if (!string.Equals(failed.Error.Code, SindriErrors.LaunchSessionWait.Code, StringComparison.Ordinal) || values is null
            || !int.TryParse(values.GetValueOrDefault("running"), NumberStyles.None, CultureInfo.InvariantCulture, out var running) || !int.TryParse(values.GetValueOrDefault("cap"), NumberStyles.None, CultureInfo.InvariantCulture, out var cap))
        {
            return;
        }

        var payload = new LaunchWaiting(request.Launch, request.Item, running, cap, request.Instance, request.Step?.Ref.State, request.Run?.Number);
        _ = await Append(events.Entry(EventTypes.LaunchWaiting, payload, "launch.waiting:" + request.Launch.Value, request.Item.Value), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Records that a skill block's session ended a turn needing the owner (skill flows spec §7): one 'launch.owner-waiting' per turn end, keyed by the turn end's time so a retried watch records nothing twice.</summary>
    /// <param name="watch">The skill launch.</param>
    /// <param name="row">Why the owner is needed, and what the reader looked at.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Done; throws when the ledger refuses, so the activity retries.</returns>
    public async Task OwnerWaiting(SkillWatch watch, OwnerWaitingRow row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(watch);
        ArgumentNullException.ThrowIfNull(row);
        var launch = LaunchId.From(watch.Launch);
        var payload = new LaunchOwnerWaiting(launch, watch.Item, row.Reason.Wire(), row.Text, row.ResultPath, row.Detail,
            watch.Instance is { } instance ? InstanceAddress.From(instance) : null, watch.State, watch.Attempt);
        var key = string.Create(CultureInfo.InvariantCulture, $"launch.owner-waiting:{launch.Value}:{row.TurnEnded.ToUnixTimeMilliseconds()}");
        _ = await Append(events.Entry(EventTypes.LaunchOwnerWaiting, payload, key, watch.Item.Value), cancellationToken).ConfigureAwait(false);
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
