namespace Aw.Sindri.Core.Profile;

/// <summary>A role of a repo's <c xml:lang="en">index.roles</c> with how the profile treats it.</summary>
/// <param name="Name">The role's name.</param>
/// <param name="Demoted">True when <c xml:lang="en">index.demote</c> names it; false for an ignored role.</param>
/// <param name="Globs">The role's repo-relative globs.</param>
public sealed record ProfileRole(string Name, bool Demoted, IReadOnlyList<string> Globs);
