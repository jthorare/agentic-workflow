using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>The item a launch ran for and that item's repo.</summary>
/// <param name="Item">The item.</param>
/// <param name="Repo">The item's repo, or null when it has none.</param>
public sealed record LaunchOwner(ItemId Item, RepoName? Repo);
