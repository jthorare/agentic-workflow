namespace Aw.Sindri.Core.Forge;

/// <summary>A pull request as the snapshot Step reads it.</summary>
/// <param name="Link">The pull request's link.</param>
/// <param name="State">Open, closed or merged.</param>
/// <param name="HeadSha">The head branch's tip now.</param>
/// <param name="Mergeable">Whether the forge says it merges cleanly; false is a conflict, null while the forge computes.</param>
/// <param name="ReviewDecision">'APPROVED', 'CHANGES_REQUESTED' or null.</param>
/// <param name="UnresolvedThreads">How many review threads are open.</param>
/// <param name="Checks">The checks.</param>
/// <param name="Labels">The labels on it, by name.</param>
/// <param name="Base">The base branch it targets, by name.</param>
/// <param name="BaseSha">The base branch's tip now, or null when the forge does not say.</param>
/// <param name="Title">The title; empty where the port does not read it. Untrusted text.</param>
/// <param name="Author">The author's login; empty where the port does not read it. Untrusted text.</param>
/// <param name="Draft">Whether it is a draft.</param>
/// <param name="HeadRef">The head branch's name; empty where the port does not read it. Untrusted text.</param>
/// <param name="Body">The body, or null where the port does not read it. Untrusted text.</param>
public sealed record PullView(string Link, PullState State, string HeadSha, bool? Mergeable, string? ReviewDecision, int UnresolvedThreads, PullChecks Checks, IReadOnlyList<string> Labels, string Base, string? BaseSha = null, string Title = "", string Author = "", bool Draft = false, string HeadRef = "", string? Body = null);
