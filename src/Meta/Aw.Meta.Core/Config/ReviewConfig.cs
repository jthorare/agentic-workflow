using System.Text.Json.Serialization;

namespace Aw.Meta.Core.Config;

/// <summary>The team's reviewer lane: when set, a pull request is mergeable on the author lane's counts with no gate run, and on
/// that lane's OK at the head too in the default mode.</summary>
/// <param name="Lane">A lane name from the team's lanes; its session name is "team-lane".</param>
/// <param name="Mode">"ok" (the default; a file without the key reads so): the reviewer's standing OK at the head is required.
/// "counts": no OK is needed, the ready line and its counts decide, and a standing HOLD from the reviewer still refuses. Any other
/// value is AW-META-003 at load.</param>
public sealed record ReviewConfig(string Lane, string Mode = ReviewConfig.OkMode)
{
    /// <summary>The reviewer's OK at the head is required (the default).</summary>
    public const string OkMode = "ok";

    /// <summary>The ready line's counts are enough; the reviewer only vetoes with a HOLD.</summary>
    public const string CountsMode = "counts";

    /// <summary>Whether the ready line's counts are enough without an OK.</summary>
    [JsonIgnore]
    public bool IsCounts => string.Equals(Mode, CountsMode, StringComparison.Ordinal);
}
