using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.LaneRecycle;

/// <summary>The refusals before a recycle, and whether routing.lanes sends the lane to another agent. The refusals: an unknown lane, the meta lane without --detach, another lane with --detach or --wait-idle, a lane that isn't Claude Code, a lane with no session, and a digest that is missing, empty or old.</summary>
/// <param name="files">The lane records.</param>
/// <param name="sessions">The session the lane's Claude runs now.</param>
/// <param name="digests">The digest files.</param>
/// <param name="context">Measures the lane before the recycle.</param>
/// <param name="clock">The current time.</param>
internal sealed class RecycleGuard(ILaneFiles files, ILaneSessions sessions, IDigestFiles digests, LaneContextReader context, TimeProvider clock)
{
    internal static readonly TimeSpan DigestMaxAge = TimeSpan.FromHours(2);

    internal async ValueTask<Result<RecyclePlan>> Check(TeamConfig team, LaneRecycleRequest request, CancellationToken cancellationToken)
    {
        var named = LaneNamed(team, request);
        if (named.IsError)
        {
            return named.Error;
        }

        var name = named.Value;

        var record = await files.Record(name, cancellationToken).ConfigureAwait(false);
        if (record.IsError)
        {
            return record.Error;
        }

        if (record.Value is { IsNonClaude: true } other)
        {
            return MetaErrors.RecycleRefused.With($"lane {name} runs {other.Agent?.Agent}: only Claude Code lanes are recycled");
        }

        // Only the live id counts: the id lane-sessions holds goes stale at every /clear, and taking it as the old id would let the poll accept the pre-clear session as the new one.
        var live = await sessions.Current(team.Team + "-" + name, cancellationToken).ConfigureAwait(false);
        if (live.IsError || live.Value is not { } session)
        {
            return MetaErrors.RecycleRefused.With($"lane {name}: no live Claude session found under tmux session {team.Team}-{name} (is the lane up?); nothing was sent");
        }

        var digest = await CheckDigest(Path.GetFullPath(request.Digest), record.Value?.RecordedAt, cancellationToken).ConfigureAwait(false);
        if (digest.IsError)
        {
            return digest.Error;
        }

        var before = await context.ReadLane(team.Team, name, team.Context.RecycleAt, cancellationToken).ConfigureAwait(false);
        var (to, model) = Routed(team, name, record.Value);
        return new RecyclePlan(team.Team, name, session, before.IsSuccess ? before.Value.Tokens : null, Path.GetFullPath(request.Digest), to, model);
    }

    // The lane as the team config spells it. The meta runs this command from inside its own turn: a direct /clear would cut that turn off, so it recycles only through the detached waiter.
    private static Result<string> LaneNamed(TeamConfig team, LaneRecycleRequest request)
    {
        var name = team.Lanes.FirstOrDefault(l => string.Equals(l, request.Lane, StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return MetaErrors.UnknownLane.With($"'{request.Lane}' is not a lane of team {team.Team}");
        }

        var isMeta = string.Equals(name, team.MetaLane, StringComparison.OrdinalIgnoreCase);
        if (isMeta && !request.Detach && !request.WaitIdle)
        {
            return MetaErrors.RecycleRefused.With($"the meta lane recycles only with --detach: a waiter then clears it once its turn has ended (lane {name})");
        }

        // #2648: the waiter waits for a stop beat, and only the meta's own Stop hook records one, so a waiter for another lane sat 30 min and recycled nothing.
        return !isMeta && (request.Detach || request.WaitIdle)
            ? MetaErrors.RecycleRefused.With($"--detach and --wait-idle are for the meta lane only: lane {name} records no stop beat for a waiter to see; run aw-meta lane-recycle {name} --digest <path> without the flag")
            : name;
    }

    // The agent routing.lanes assigns when it differs from the one the lane runs (the lane is Claude Code here), else none.
    private static (string? Agent, string? Model) Routed(TeamConfig team, string lane, LaneRecord? record)
    {
        if (!team.Routing.Lanes.TryGetValue(lane, out var route))
        {
            return (null, null);
        }

        var running = record?.Agent?.Agent ?? team.Agent ?? LaneAgent.ClaudeCode;
        var assigned = route.Agent ?? (route.Model is { } model ? team.Routing.AgentOf(model) : null);
        return assigned is not null && !string.Equals(assigned, running, StringComparison.Ordinal) ? (assigned, route.Model) : (null, null);
    }

    // A digest the lane's current session wrote is newer than the time its session id was recorded (the last recycle or the
    // session-id hook). One path serves a lane all day, so an older file is the previous session's digest (#2661).
    private async ValueTask<Result<Unit>> CheckDigest(string path, DateTimeOffset? sessionSince, CancellationToken cancellationToken)
    {
        var stat = await digests.Stat(path, cancellationToken).ConfigureAwait(false);
        if (stat.IsError)
        {
            return stat.Error;
        }

        return stat.Value switch
        {
            null => MetaErrors.RecycleRefused.With($"the digest {path} does not exist"),
            { Length: 0 } => MetaErrors.RecycleRefused.With($"the digest {path} is empty"),
            { } info when clock.GetUtcNow() - info.ModifiedAt > DigestMaxAge => MetaErrors.RecycleRefused.With($"the digest {path} is older than 2 hours: ask the lane for a fresh one"),
            { } info when sessionSince is { } since && info.ModifiedAt < since => MetaErrors.RecycleRefused.With(
                string.Create(CultureInfo.InvariantCulture, $"the digest {path} was last written at {info.ModifiedAt.UtcDateTime:yyyy-MM-dd HH:mm:ss}Z, before the lane's current session started at {since.UtcDateTime:yyyy-MM-dd HH:mm:ss}Z: it is the previous session's digest; wait for the lane's new write, or ask it for a fresh one")),
            _ => Unit.Value,
        };
    }
}
