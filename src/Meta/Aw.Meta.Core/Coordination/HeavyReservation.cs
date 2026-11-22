using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>
/// Works out the heavy-slot reservation from the merge-queue depth (the ready pull requests not yet merged) and keeps it in the
/// team's own state directory. It runs wherever the queue changes (a gate run, a merge, a queue-drop, each watch poll, status).
/// aw-meta never writes the heavy-job wrapper's files: the meta runs meta-gate-reserve with the argument shown (the script's own tiers differ from the team's) before each gate. Each
/// change of the reservation is one NOTE line in the running log. A team that sets "heavy": {"reserve": false} computes nothing.
/// </summary>
/// <param name="reserve">The team's kept reservation.</param>
/// <param name="queue">Reads the merge queue.</param>
/// <param name="log">The running log.</param>
/// <param name="config">The team config, for callers that have not loaded it.</param>
/// <param name="clock">Dates the computation and the log line.</param>
public sealed class HeavyReservation(IHeavyReserve reserve, MergeQueueReader queue, IRunningLog log, IMetaConfig config, TimeProvider clock)
{
    /// <summary>The same reservation over other gh calls (a poll's, bound by its limit and the stop signal).</summary>
    /// <param name="github">The calls to use.</param>
    /// <returns>A reservation over them.</returns>
    public HeavyReservation Using(RepoCommands github) => new(reserve, queue.Using(github), log, config, clock);

    /// <summary>A computation older than this is stale: the queue may have changed with nobody recomputing.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

    // An unchanged computation is kept as it is for this long; after it the time is renewed, so a running watch keeps it fresh.
    private static readonly TimeSpan _renewAfter = TimeSpan.FromMinutes(5);

    /// <summary>Recomputes the reservation for the queue as it stands, loading the team config.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The reservation, null when the team turned it off, or the config's, the store's or the file's error.</returns>
    public async ValueTask<Result<HeavySlots?>> Apply(CancellationToken cancellationToken)
    {
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        return team.IsError ? team.Error : await Apply(team.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Recomputes the reservation for the queue as it stands.</summary>
    /// <param name="team">The team config (its reserve tiers and cap).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The reservation, null when the team turned it off, or the store's or the file's error.</returns>
    public async ValueTask<Result<HeavySlots?>> Apply(TeamConfig team, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(team);
        var setting = team.Heavy.EffectiveReserve;
        if (!setting.Enabled)
        {
            return Results.Success<HeavySlots?>(value: null);
        }

        var computed = await Compute(setting, team.Heavy.Cap, cancellationToken).ConfigureAwait(false);
        if (computed.IsError)
        {
            return computed.Error;
        }

        var (kept, wanted) = computed.Value;
        return kept is { } same && Unchanged(same, wanted)
            ? Results.Success<HeavySlots?>(new HeavySlots(team.Heavy.Cap, same.Reserve, same.Depth, same.At))
            : await Keep(kept, wanted, team.Heavy.Cap, cancellationToken).ConfigureAwait(false);
    }

    // What is kept, and what the queue as it stands calls for.
    private async ValueTask<Result<(HeavyReserveRecord? Kept, HeavyReserveRecord Wanted)>> Compute(ReserveSetting setting, int cap, CancellationToken cancellationToken)
    {
        var depth = await queue.Read(cancellationToken).ConfigureAwait(false);
        var kept = await reserve.Read(cancellationToken).ConfigureAwait(false);
        if (depth.IsError || kept.IsError)
        {
            return depth.IsError ? depth.Error : kept.Error;
        }

        return (kept.Value, new HeavyReserveRecord(setting.For(depth.Value.Count, cap), depth.Value.Count, clock.GetUtcNow()));
    }

    private async ValueTask<Result<HeavySlots?>> Keep(HeavyReserveRecord? kept, HeavyReserveRecord wanted, int cap, CancellationToken cancellationToken)
    {
        var written = await reserve.Write(wanted, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return written.Error;
        }

        var slots = new HeavySlots(cap, wanted.Reserve, wanted.Depth, wanted.At);
        var before = kept?.Reserve ?? 0;
        var logged = before == wanted.Reserve ? Results.Ok() : await Note(before, slots, cancellationToken).ConfigureAwait(false);
        return logged.IsError ? logged.Error : Results.Success<HeavySlots?>(slots);
    }

    // The same reservation for the same depth, computed recently enough that renewing the time is not yet due.
    private static bool Unchanged(HeavyReserveRecord kept, HeavyReserveRecord wanted) =>
        kept.Reserve == wanted.Reserve && kept.Depth == wanted.Depth && wanted.At - kept.At < _renewAfter;

    private async ValueTask<Result<Unit>> Note(int before, HeavySlots slots, CancellationToken cancellationToken)
    {
        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"heavy reservation {before} -> {slots.Reserve} of {slots.Cap} slots (merge queue depth {slots.Depth}); set it with meta-gate-reserve {slots.ScriptDepth}");
        return await log.Append(LogLine.Event(LogKind.Note, slots.ComputedAt, text), cancellationToken).ConfigureAwait(false);
    }
}
