namespace Aw.Context.Core.Roles;

/// <summary>How many tracked files one ignored role of a repo dropped when a map version was built (stored with the version: the files are never indexed, so nothing else can count them later).</summary>
/// <param name="Repo">The repo's name.</param>
/// <param name="Role">The ignored role's name.</param>
/// <param name="Files">Tracked files at the commit the build read that matched one of the role's globs.</param>
public sealed record RoleDropped(string Repo, string Role, int Files);
