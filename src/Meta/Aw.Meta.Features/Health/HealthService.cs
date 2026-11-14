using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Health;

/// <summary>
/// Checks the box: memory by kind of process, orphaned Temporal dev servers (killed by pid with the flag), duplicate MCP
/// servers, lanes with no live session and who holds the heavy-job lock. The result is kept for status.
/// </summary>
/// <param name="probe">Reads the host.</param>
/// <param name="lanes">Finds missing lanes.</param>
/// <param name="killer">Terminates orphans.</param>
/// <param name="values">Keeps the summary.</param>
/// <param name="config">The team config, for the thresholds (the defaults without one).</param>
/// <param name="advisor">Shows the kept heavy reservation and computes the builders advice.</param>
/// <param name="clock">Dates the summary.</param>
internal sealed class HealthService(HostProbe probe, LaneCheck lanes, OrphanKiller killer, IKeyValueStore values, IMetaConfig config, ResourceAdvisor advisor, TimeProvider clock)
    : IService<HealthRequest, HealthResponse>
{
    public async ValueTask<Result<HealthResponse>> Handle(HealthRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var host = await probe.Read(cancellationToken).ConfigureAwait(false);
        var missing = await lanes.Missing(cancellationToken).ConfigureAwait(false);
        var standing = await lanes.Standing(cancellationToken).ConfigureAwait(false);
        if (host.IsError || missing.IsError || standing.IsError)
        {
            return host.IsError ? host.Error : missing.IsError ? missing.Error : standing.Error;
        }

        var orphans = HostRules.Orphans(host.Value.Snapshot.Processes);
        var killed = request.KillOrphans ? await killer.Kill(orphans, cancellationToken).ConfigureAwait(false) : Results.Success<IReadOnlyList<int>>([]);
        if (killed.IsError)
        {
            return killed.Error;
        }

        return await Report(host.Value, new Findings(orphans, killed.Value, missing.Value, standing.Value), cancellationToken).ConfigureAwait(false);
    }

    private sealed record Findings(IReadOnlyList<ProcessInfo> Orphans, IReadOnlyList<int> Killed, IReadOnlyList<string>? Missing, IReadOnlyList<Core.Lanes.LaneHealth>? Standing);

    private async ValueTask<Result<HealthResponse>> Report(HostReading host, Findings found, CancellationToken cancellationToken)
    {
        var (orphans, killed, missing, standing) = found;
        var processes = host.Snapshot.Processes;
        var remaining = orphans.Count(o => !killed.Contains(o.Pid));
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        var low = ResourceRules.MemoryLow(host.Resources, team.IsSuccess ? team.Value.Resources : new ResourcesConfig());
        var advice = await advisor.Advise(team.IsSuccess ? team.Value : null, host.Resources, refresh: false, cancellationToken).ConfigureAwait(false);
        if (advice.IsError)
        {
            return advice.Error;
        }

        // Advisory: lanes whose context can't be measured are left out; a recycle verdict does not make the box unhealthy.
        var context = await lanes.Context(cancellationToken).ConfigureAwait(false);
        var response = new HealthResponse(
            ProcessGroups.Group(processes), processes.Count, host.Resources, orphans, killed, HostRules.Duplicates(processes), missing, host.Locks, low, NeedsAttention(remaining, low, missing, standing), standing, advice.Value.Heavy, advice.Value.Builders, context)
        { Consumer = await Consumer(cancellationToken).ConfigureAwait(false), OomPolicy = host.OomPolicy, StaleScripts = host.StaleScripts };
        var kept = await values.Set(KvKeys.HealthLast, Summary(response, remaining), cancellationToken).ConfigureAwait(false);
        return kept.IsError ? kept.Error : response;
    }

    // Health reads no mail, so the line says whether a watch beats and never raises attention; status adds the waiting count.
    private async ValueTask<string?> Consumer(CancellationToken cancellationToken)
    {
        var line = await ConsumerState.Read(values, waiting: null, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return line.IsError ? null : line.Value.Line;
    }

    private static bool NeedsAttention(int orphans, bool memoryLow, IReadOnlyList<string>? missingLanes, IReadOnlyList<Core.Lanes.LaneHealth>? standing) =>
        orphans > 0 || memoryLow || missingLanes is { Count: > 0 } || standing?.Any(static l => l.Attention) == true;

    private string Summary(HealthResponse health, int orphans) => string.Create(
        CultureInfo.InvariantCulture,
        $"{clock.GetUtcNow():O} {(health.Attention ? "attention" : "ok")}: orphans={orphans} missing-lanes={(health.MissingLanes is null ? "-" : health.MissingLanes.Count)} memory-available={health.Host.AvailablePercent}%");
}
