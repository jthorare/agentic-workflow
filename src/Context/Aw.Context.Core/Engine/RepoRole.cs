namespace Aw.Context.Core.Engine;

/// <summary>A role of a repo as the profile names it (<c xml:lang="en">index.roles</c>) and treats it (<c xml:lang="en">index.ignore</c> or <c xml:lang="en">index.demote</c>).</summary>
/// <param name="Name">The role's name.</param>
/// <param name="Demoted">True for a demoted role (its files stay indexed); false for an ignored one (its files are not indexed).</param>
/// <param name="Globs">The role's repo-relative globs.</param>
public sealed record RepoRole(string Name, bool Demoted, IReadOnlyList<string> Globs);
