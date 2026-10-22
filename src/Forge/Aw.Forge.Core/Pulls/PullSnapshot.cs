using Aw.Forge.Core.Refs;

namespace Aw.Forge.Core.Pulls;

/// <summary>A pull request's state: what the babysitter's snapshot reads from the forge.</summary>
/// <param name="Ref">The pull request.</param>
/// <param name="Url">Its link.</param>
/// <param name="Title">The title.</param>
/// <param name="Author">The author's login.</param>
/// <param name="Status">Open, closed or merged.</param>
/// <param name="Draft">Whether it is a draft.</param>
/// <param name="Base">The base branch.</param>
/// <param name="Head">The head branch.</param>
/// <param name="Mergeable">Whether the forge says it merges cleanly; null while it computes.</param>
/// <param name="ReviewDecision">APPROVED, CHANGES_REQUESTED or null.</param>
/// <param name="Checks">The checks on the head.</param>
/// <param name="Labels">The labels on it, by name.</param>
public sealed record PullSnapshot(
    ForgeRef Ref,
    string Url,
    string Title,
    string Author,
    PullStatus Status,
    bool Draft,
    BaseBranch Base,
    HeadBranch Head,
    bool? Mergeable,
    string? ReviewDecision,
    ChecksSummary Checks,
    IReadOnlyList<string> Labels);
