namespace Aw.Sindri.Core.Forge;

/// <summary>What <see cref="IPullRequests.Find"/> looks for: the open pull request whose head is a branch and whose body carries a marker.</summary>
/// <param name="Repo">The repository.</param>
/// <param name="Head">The head branch.</param>
/// <param name="Marker">The marker the body carries (letters, digits, '.', '_', ':' and '-'), or null to match on the head alone.</param>
public sealed record FindPull(PullRepo Repo, string Head, string? Marker);
