using Aw.Meta.Core.Ids;

namespace Aw.Meta.Features.LaneRecycle;

// What a recycle will act on, once every refusal has been checked. SwitchTo is the agent routing.lanes assigns when it is not the one the lane runs.
internal sealed record RecyclePlan(string Team, string Lane, SessionId OldSession, long? OldTokens, string Digest, string? SwitchTo = null, string? SwitchModel = null)
{
    internal string Session => Team + "-" + Lane;

    internal string ContinueLine => "Read and continue from " + Digest;

    // When the digest was last written and when the lane's current session id was recorded (null when the record has no time), for --dry-run (#2661).
    internal DateTimeOffset? DigestWrittenAt { get; init; }

    internal DateTimeOffset? SessionSince { get; init; }

    internal string? DigestTimes => DigestWrittenAt is { } written
        ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"digest last written {written.UtcDateTime:yyyy-MM-dd HH:mm:ss}Z")
            + (SessionSince is { } since ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"; lane's current session since {since.UtcDateTime:yyyy-MM-dd HH:mm:ss}Z") : "; lane's session start not recorded")
        : null;
}
