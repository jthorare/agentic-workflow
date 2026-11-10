namespace Aw.Meta.Core.Merge;

/// <summary>What the inbox says about a pull request when the team has a reviewer lane.</summary>
public sealed record ReviewFacts
{
    /// <summary>The reviewer lane's session name.</summary>
    public required string Reviewer { get; init; }

    /// <summary>The sha of the newest "OK #pr at sha" line the reviewer sent for the pull request; null when there is none.</summary>
    public string? OkSha { get; init; }

    /// <summary>The sha on the first line of the newest ready message for the pull request; null when there is no such message.</summary>
    public string? ReadySha { get; init; }

    /// <summary>The count lines of that ready message.</summary>
    public IReadOnlyList<CountLine> Counts { get; init; } = [];
}
