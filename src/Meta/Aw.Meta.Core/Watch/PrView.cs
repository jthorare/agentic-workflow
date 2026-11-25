namespace Aw.Meta.Core.Watch;

/// <summary>A pull request as one poll saw it.</summary>
/// <param name="Number">The number.</param>
/// <param name="State">Open, closed or merged.</param>
/// <param name="IsDraft">Whether it is a draft.</param>
/// <param name="Head">The head commit.</param>
/// <param name="Mergeable">MERGEABLE, CONFLICTING or UNKNOWN.</param>
public sealed record PrView(int Number, PrState State, bool IsDraft, string Head, string Mergeable)
{
    /// <summary>The head branch name; empty when the poll did not ask for it.</summary>
    public string HeadBranch { get; init; } = "";

    /// <summary>The base branch name; empty when the poll did not ask for it.</summary>
    public string BaseBranch { get; init; } = "";
}
