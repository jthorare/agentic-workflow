namespace Aw.Sindri.Core.Forge;

/// <summary>A pull request as the snapshot Step reads it.</summary>
/// <param name="Link">The pull request's link.</param>
/// <param name="State">Open, closed or merged.</param>
/// <param name="HeadSha">The head branch's tip now.</param>
/// <param name="Mergeable">Whether the forge says it merges cleanly; false is a conflict, null while the forge computes.</param>
/// <param name="ReviewDecision">'APPROVED', 'CHANGES_REQUESTED' or null.</param>
/// <param name="UnresolvedThreads">How many review threads are open.</param>
/// <param name="Checks">The checks.</param>
public sealed record PullView(string Link, PullState State, string HeadSha, bool? Mergeable, string? ReviewDecision, int UnresolvedThreads, PullChecks Checks);
