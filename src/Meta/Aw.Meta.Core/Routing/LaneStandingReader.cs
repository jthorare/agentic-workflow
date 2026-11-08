using Aw.Meta.Core.Config;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Usage;

namespace Aw.Meta.Core.Routing;

/// <summary>Adds each lane's provider, model and usage windows to what the usage look found.</summary>
/// <param name="files">The lane records.</param>
/// <param name="rollouts">Codex's rollout files.</param>
/// <param name="clock">The current time.</param>
public sealed class LaneStandingReader(ILaneFiles files, ICodexRollouts rollouts, TimeProvider clock)
{
    /// <summary>The most of a rollout's end that is read.</summary>
    public const int TailBytes = 256 * 1024;

    private sealed record Look(ProviderSignal? Windows, string? Note);

    private sealed record Placement(string Running, string Assigned, string? Model, bool IsMeta);

    /// <summary>Builds one standing per subject, the meta included.</summary>
    /// <param name="team">The team.</param>
    /// <param name="readings">The usage look.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The standings, in the readings' order.</returns>
    public async ValueTask<IReadOnlyList<LaneStanding>> Read(TeamConfig team, IReadOnlyList<UsageReading> readings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(readings);
        var rows = new List<LaneStanding>();
        foreach (var reading in readings)
        {
            rows.Add(string.Equals(reading.Subject, UsageWatch.Meta, StringComparison.Ordinal)
                ? Standing(reading, new Placement(MetaAgent(team), MetaAgent(team), Model: null, IsMeta: true), new Look(Windows: null, Note: null))
                : await Lane(team, reading, cancellationToken).ConfigureAwait(false));
        }

        return rows;
    }

    private static string MetaAgent(TeamConfig team) => team.Agent ?? LaneAgent.ClaudeCode;

    private static LaneStanding Standing(UsageReading reading, Placement place, Look look) =>
        new(reading.Subject, place.Assigned, place.Running, place.Model, reading.State, reading.Stop?.Kind, reading.Stop?.ResetsAt, look.Windows, look.Note, place.IsMeta);

    private async ValueTask<LaneStanding> Lane(TeamConfig team, UsageReading reading, CancellationToken cancellationToken)
    {
        var read = await files.Record(reading.Subject, cancellationToken).ConfigureAwait(false);
        var record = read.IsSuccess ? read.Value : null;
        var running = record?.Agent?.Agent ?? MetaAgent(team);
        var routing = team.Routing;
        var route = routing.Lanes.GetValueOrDefault(reading.Subject);
        var assigned = route?.Agent ?? (route?.Model is { } named ? routing.AgentOf(named) : null) ?? running;
        var look = await SignalOf(running, record, cancellationToken).ConfigureAwait(false);
        return Standing(reading, new Placement(running, assigned, ModelOf(routing, route, assigned), IsMeta: false), look);
    }

    // The lane's own model; else the default, or its equivalent when the default is on another provider; else unknown.
    private static string? ModelOf(RoutingConfig routing, LaneRoute? route, string agent)
    {
        if (route?.Model is { } own)
        {
            return own;
        }

        return routing.DefaultModel is { } fallback
            ? OnAgent(routing, fallback, agent) ?? (routing.EquivalentOf(fallback) is { } other ? OnAgent(routing, other, agent) : null)
            : null;
    }

    private static string? OnAgent(RoutingConfig routing, string model, string agent) =>
        string.Equals(routing.AgentOf(model), agent, StringComparison.Ordinal) ? model : null;

    private async ValueTask<Look> SignalOf(string agent, LaneRecord? record, CancellationToken cancellationToken)
    {
        if (string.Equals(agent, LaneAgent.ClaudeCode, StringComparison.Ordinal))
        {
            return new Look(Windows: null, "Claude Code shows only the limit stop, not a remaining share");
        }

        if (!string.Equals(agent, "codex", StringComparison.Ordinal))
        {
            return new Look(Windows: null, $"{agent} shows no usage figure");
        }

        return record?.Session is { } session
            ? await CodexSignal(session, cancellationToken).ConfigureAwait(false)
            : new Look(Windows: null, "the lane has reported no session id yet");
    }

    private async ValueTask<Look> CodexSignal(Ids.SessionId session, CancellationToken cancellationToken)
    {
        var tail = await rollouts.Tail(session, TailBytes, cancellationToken).ConfigureAwait(false);
        if (tail.IsError)
        {
            return new Look(Windows: null, "the Codex rollout can't be read: " + tail.Error.Message.ReplaceLineEndings(" "));
        }

        if (tail.Value is not { } read)
        {
            return new Look(Windows: null, "no Codex rollout found for the lane's session");
        }

        return CodexRateLimits.Read(read.Lines, clock.GetUtcNow()) is { } windows
            ? new Look(windows, Note: null)
            : new Look(Windows: null, "the Codex rollout has no rate_limits line yet");
    }
}
