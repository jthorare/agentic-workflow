namespace Aw.Meta.Core.Merge;

/// <summary>What the inbox says about a pull request when the team has a reviewer lane.</summary>
public sealed record ReviewFacts
{
    /// <summary>The reviewer lane's session name.</summary>
    public required string Reviewer { get; init; }

    /// <summary>The sha of the reviewer's standing "OK #pr at sha" line: the newest one not cleared by a later "HOLD #pr"; null when there is none.</summary>
    public string? OkSha { get; init; }

    /// <summary>The reviewer's standing "HOLD #pr" line: set when the newest of its OK and HOLD lines for the pull request is a HOLD; null otherwise.</summary>
    public string? Hold { get; init; }

    /// <summary>Whether the team's review.mode is "counts": no OK is needed, only a standing HOLD refuses.</summary>
    public bool CountsMode { get; init; }

    /// <summary>Whether the line after that OK says "qa not yet": a UI pull request qa hasn't passed.</summary>
    public bool QaNotYet { get; init; }

    /// <summary>The sha of a "qa passed sha" line right after that OK; null when there is none (no qa requirement).</summary>
    public string? QaSha { get; init; }

    /// <summary>The sha on the first line of the newest ready message for the pull request; null when there is no such message.</summary>
    public string? ReadySha { get; init; }

    /// <summary>Whether that ready message has a line that is exactly "0 passed, 0 skipped, 0 failed (build only)" once trimmed, in any case: the lane built and ran no test. In review.mode counts it stands in for a count line that passed tests.</summary>
    public bool BuildOnly { get; init; }

    /// <summary>The count lines of that ready message.</summary>
    public IReadOnlyList<CountLine> Counts { get; init; } = [];
}
