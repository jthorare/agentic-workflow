using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.LaneRecycle;

/// <summary>The refusals before a recycle, and whether routing.lanes sends the lane to another agent. The refusals: an unknown lane, a lane that isn't Claude Code, a lane with no session, and a digest that is missing, empty or old.</summary>
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
        var name = team.Lanes.FirstOrDefault(l => string.Equals(l, request.Lane, StringComparison.OrdinalIgnoreCase) && !string.Equals(l, team.MetaLane, StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return MetaErrors.UnknownLane.With($"'{request.Lane}' is not a lane of team {team.Team}");
        }

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

        var digest = await CheckDigest(Path.GetFullPath(request.Digest), cancellationToken).ConfigureAwait(false);
        if (digest.IsError)
        {
            return digest.Error;
        }

        var before = await context.ReadLane(team.Team, name, team.Context.RecycleAt, cancellationToken).ConfigureAwait(false);
        var (to, model) = Routed(team, name, record.Value);
        return new RecyclePlan(team.Team, name, session, before.IsSuccess ? before.Value.Tokens : null, Path.GetFullPath(request.Digest), to, model);
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

    private async ValueTask<Result<Unit>> CheckDigest(string path, CancellationToken cancellationToken)
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
            _ => Unit.Value,
        };
    }
}
