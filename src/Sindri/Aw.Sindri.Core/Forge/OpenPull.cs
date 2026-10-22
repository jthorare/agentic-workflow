namespace Aw.Sindri.Core.Forge;

/// <summary>A pull request to open with <see cref="IPullRequests.Open"/>.</summary>
/// <param name="Repo">The repository.</param>
/// <param name="Head">The head branch, already on the remote.</param>
/// <param name="Base">The branch to merge into.</param>
/// <param name="HeadSha">The commit the head branch is at.</param>
/// <param name="Title">The title.</param>
/// <param name="Body">The body, without the marker.</param>
/// <param name="Draft">Whether it opens as a draft.</param>
/// <param name="Marker">The marker added to the body so Find recognises the pull request, or null for none.</param>
public sealed record OpenPull(PullRepo Repo, string Head, string Base, string HeadSha, string Title, string Body, bool Draft, string? Marker);
