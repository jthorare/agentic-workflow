using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>Measures each lane's context window from the end of its session transcript.</summary>
/// <param name="files">The lane records (the session id a lane's hooks reported, the fallback).</param>
/// <param name="sessions">The session each Claude lane runs now.</param>
/// <param name="transcripts">Claude Code's transcripts.</param>
public sealed class LaneContextReader(ILaneFiles files, ILaneSessions sessions, IClaudeTranscripts transcripts)
{
    /// <summary>How many bytes from the end of a transcript are read: the last API call is in the last few lines.</summary>
    public const int TailBytes = 1024 * 1024;

    /// <summary>Reads every lane, the meta lane included.</summary>
    /// <param name="team">The team config.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One row per lane, in lane order.</returns>
    public async ValueTask<Result<IReadOnlyList<LaneContext>>> Read(TeamConfig team, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(team);
        var rows = new List<LaneContext>();
        foreach (var lane in team.Lanes)
        {
            var row = await ReadLane(team.Team, lane, team.Context.RecycleAt, cancellationToken).ConfigureAwait(false);
            if (row.IsError)
            {
                return row.Error;
            }

            rows.Add(row.Value);
        }

        return rows;
    }

    /// <summary>Reads every lane for a report that can do without: a failure to read leaves the figures out.</summary>
    /// <param name="team">The team config; null when there is none.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One row per lane, or null.</returns>
    public async ValueTask<IReadOnlyList<LaneContext>?> ReadOrNull(TeamConfig? team, CancellationToken cancellationToken)
    {
        if (team is null)
        {
            return null;
        }

        var rows = await Read(team, cancellationToken).ConfigureAwait(false);
        return rows.IsSuccess ? rows.Value : null;
    }

    /// <summary>Reads one lane.</summary>
    /// <param name="team">The team name (tmux sessions are "team-lane").</param>
    /// <param name="lane">The lane name.</param>
    /// <param name="threshold">The recycle threshold.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The lane's context.</returns>
    public async ValueTask<Result<LaneContext>> ReadLane(string team, string lane, int threshold, CancellationToken cancellationToken)
    {
        var record = await files.Record(lane, cancellationToken).ConfigureAwait(false);
        if (record.IsError)
        {
            return record.Error;
        }

        if (record.Value is { IsNonClaude: true } other)
        {
            return Unknown(lane, other.Session, threshold, $"a {other.Agent?.Agent} lane: only Claude Code transcripts are measured");
        }

        // Claude Code changes the session id on /clear and nothing updates lane-sessions for a Claude lane: the live id comes first.
        var live = await sessions.Current(team + "-" + lane, cancellationToken).ConfigureAwait(false);
        var known = (live.IsSuccess ? live.Value : null) ?? record.Value?.Session;
        if (known is not { } session)
        {
            return Unknown(lane, session: null, threshold, "no session id found for the lane");
        }

        return await Measure(lane, session, threshold, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<LaneContext>> Measure(string lane, Ids.SessionId session, int threshold, CancellationToken cancellationToken)
    {
        var tail = await transcripts.Tail(session, TailBytes, cancellationToken).ConfigureAwait(false);
        if (tail.IsError)
        {
            return Unknown(lane, session, threshold, "the transcript can't be read: " + tail.Error.Message.ReplaceLineEndings(" "));
        }

        if (tail.Value is not { } read)
        {
            return Unknown(lane, session, threshold, "no transcript found for the session");
        }

        return TranscriptUsage.ContextTokens(read.Lines) is { } tokens
            ? new LaneContext(lane, session, read.Path, tokens, read.ModifiedAt, threshold, tokens >= threshold ? ContextVerdict.Recycle : ContextVerdict.Ok)
            : new LaneContext(lane, session, read.Path, Tokens: null, read.ModifiedAt, threshold, ContextVerdict.Unknown, "no usage in the end of the transcript");
    }

    private static LaneContext Unknown(string lane, Ids.SessionId? session, int threshold, string reason) =>
        new(lane, session, Transcript: null, Tokens: null, LastActivity: null, threshold, ContextVerdict.Unknown, reason);
}
