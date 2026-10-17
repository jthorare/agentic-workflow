using Aw.Forge.Core.Refs;

namespace Aw.Forge.Core.Pulls;

/// <summary>Looks for an open pull request this caller opened.</summary>
/// <param name="Repo">The repository.</param>
/// <param name="Head">The head branch.</param>
/// <param name="Author">The author's login, or null for any.</param>
/// <param name="Marker">A marker the body carries, or null for any.</param>
public sealed record FindPullQuery(ForgeRepo Repo, string Head, string? Author, string? Marker);
