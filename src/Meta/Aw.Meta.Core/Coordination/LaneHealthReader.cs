using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>Says how each non-Claude lane stands: taking part, launch-only, hooks not running, an obligation it can't be shown, or an ambiguous session id. Claude lanes have no row.</summary>
/// <param name="files">The lane records.</param>
/// <param name="beats">The hook heartbeats.</param>
/// <param name="mail">The messages owed.</param>
/// <param name="guard">The stop gate's counts.</param>
/// <param name="clock">Ages the undelivered mail.</param>
/// <param name="options">How long mail may stay undelivered.</param>
public sealed class LaneHealthReader(ILaneFiles files, LaneBeats beats, ILaneMail mail, LaneStopGuard guard, TimeProvider clock, LaneHealthOptions options)
{
    /// <summary>Cursor ends a stop follow-up chain after this many (its default loop_limit); the gate can't be shown past it.</summary>
    public const int CursorLoopLimit = 5;

    /// <summary>Reads every non-Claude lane's standing.</summary>
    /// <param name="team">The team config.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One row per non-Claude lane; empty for a team of Claude lanes.</returns>
    public async ValueTask<Result<IReadOnlyList<LaneHealth>>> Read(TeamConfig team, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(team);
        var rows = new List<LaneHealth>();
        foreach (var lane in team.Lanes.Where(l => !string.Equals(l, team.MetaLane, StringComparison.OrdinalIgnoreCase)))
        {
            var record = await files.Record(lane, cancellationToken).ConfigureAwait(false);
            if (record.IsError)
            {
                return record.Error;
            }

            if (record.Value is not { IsNonClaude: true, Agent: { } agent } found)
            {
                continue;
            }

            var state = await Standing(lane, found, agent, cancellationToken).ConfigureAwait(false);
            if (state.IsError)
            {
                return state.Error;
            }

            rows.Add(new LaneHealth(lane, agent.Agent, state.Value.State, state.Value.Attention));
        }

        return rows;
    }

    private async ValueTask<Result<(string State, bool Attention)>> Standing(string lane, LaneRecord record, LaneAgent agent, CancellationToken cancellationToken)
    {
        if (!agent.Hooks)
        {
            return ("launch-only: no obligations", false);
        }

        if (record.Ambiguous || record.Session is null)
        {
            return record.Ambiguous ? ("ambiguous session id", true) : ("waiting for its session id", false);
        }

        var beat = await beats.Last(lane, cancellationToken).ConfigureAwait(false);
        if (beat.IsError)
        {
            return beat.Error;
        }

        if (beat.Value is { } last && !(record.RecordedAt is { } recorded && last.At < recorded))
        {
            return await Delivery(lane, agent, cancellationToken).ConfigureAwait(false);
        }

        var why = await WhyNoHooks(files, beats, record, agent, cancellationToken).ConfigureAwait(false);
        return why.IsError ? why.Error : (why.Value, true);
    }

    /// <summary>Says why a lane has no hook heartbeat since its session id: the hooks never ran, ran and skipped the lane, failed, or only a hand-typed aw-meta call was seen.</summary>
    /// <param name="files">The lane records and hook traces.</param>
    /// <param name="beats">The heartbeats.</param>
    /// <param name="record">The lane's record.</param>
    /// <param name="agent">The lane's agent.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The line, naming the fix.</returns>
    public static async ValueTask<Result<string>> WhyNoHooks(ILaneFiles files, LaneBeats beats, LaneRecord record, LaneAgent agent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(beats);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(agent);
        var trace = await files.HookTrace(record.Lane, cancellationToken).ConfigureAwait(false);
        var cli = await beats.LastCli(record.Lane, cancellationToken).ConfigureAwait(false);
        if (trace.IsError || cli.IsError)
        {
            return trace.IsError ? trace.Error : cli.Error;
        }

        var reason = HooksNotRunning(agent, trace.Value is { } t && !(record.RecordedAt is { } at && t.At < at) ? t : null);
        return cli.Value is { } manual && !(record.RecordedAt is { } since && manual < since)
            ? "hooks not verified: only manual aw-meta runs seen (last " + Clock(manual) + "); " + reason
            : reason;
    }

    private async ValueTask<Result<(string State, bool Attention)>> Delivery(string lane, LaneAgent agent, CancellationToken cancellationToken)
    {
        var owed = await mail.Owed(lane, cancellationToken).ConfigureAwait(false);
        if (owed.IsError)
        {
            return owed.Error;
        }

        var state = await guard.State(lane, owed.Value, cancellationToken).ConfigureAwait(false);
        if (state.IsError)
        {
            return state.Error;
        }

        return state.Value switch
        {
            { Capped: true } => ("obligation not delivered (stop cap)", true),
            { Blocks: >= CursorLoopLimit } when string.Equals(agent.Agent, "cursor", StringComparison.Ordinal) => ("obligation not delivered (Cursor loop_limit)", true),
            _ => await Undelivered(lane, owed.Value, cancellationToken).ConfigureAwait(false),
        };
    }

    // A hook ran, yet the oldest message is still unseen after the limit: the lane is idle and nothing woke it.
    private async ValueTask<Result<(string State, bool Attention)>> Undelivered(string lane, IReadOnlyList<LaneMessage> owed, CancellationToken cancellationToken)
    {
        var oldest = owed.Where(static m => !m.Delivered).MinBy(static m => m.At);
        var age = oldest is null ? TimeSpan.Zero : clock.GetUtcNow() - oldest.At;
        if (oldest is null || age < options.UndeliveredAfter)
        {
            return ("taking part", false);
        }

        var trace = await files.HookTrace(lane, cancellationToken).ConfigureAwait(false);
        var beat = await beats.Last(lane, cancellationToken).ConfigureAwait(false);
        if (trace.IsError || beat.IsError)
        {
            return trace.IsError ? trace.Error : beat.Error;
        }

        var last = trace.Value is { } t ? $"the lane's last hook was {t.Event} at {Clock(t.At)}" : $"the lane's last hook beat was at {Clock(beat.Value?.At ?? oldest.At)}";
        var minutes = (int)age.TotalMinutes;
        return (string.Create(System.Globalization.CultureInfo.InvariantCulture, $"mail not delivered for {minutes} min: {last}; lane-send wakes an idle lane"), true);
    }

    /// <summary>The line for a lane whose hooks left no heartbeat, by what its hook trace says.</summary>
    /// <param name="agent">The lane's agent.</param>
    /// <param name="trace">The lane's hook trace newer than its session id; null when none.</param>
    /// <returns>The line, naming the fix.</returns>
    public static string HooksNotRunning(LaneAgent agent, LaneHookTrace? trace = null)
    {
        ArgumentNullException.ThrowIfNull(agent);
        var seen = trace is null ? null : $"{trace.Event} at {Clock(trace.At)}";
        return trace?.Outcome switch
        {
            "not-own-agent" => $"hooks ran but skipped the lane: the hook's process is not under the lane's agent ({seen})",
            "aw-meta-missing" => "hooks ran but found no aw-meta binary (scripts/install-meta.sh)",
            "ran" => $"hooks ran ({seen}) but recorded no heartbeat",
            { } outcome when outcome.EndsWith("-failed", StringComparison.Ordinal) => $"hooks ran but aw-meta {outcome[..^"-failed".Length]} failed at {Clock(trace.At)} (see logs/failopen.log)",
            _ => agent.Agent switch
            {
                "codex" => "hooks never ran: trust them with /hooks in codex (each install that changes a hook asks again)",
                "cursor" => "hooks never ran: check ~/.cursor/hooks.json has the aw:meta-lane-gate entries (scripts/install-meta.sh)",
                _ => "hooks never ran: check the " + agent.Agent + " hooks are installed",
            },
        };
    }

    private static string Clock(DateTimeOffset at) => at.UtcDateTime.ToString("HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);
}
