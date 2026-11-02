namespace Aw.Context.Core.Roles;

/// <summary>Indexed files of a repo that look like a role (<see cref="RoleLikePaths"/>) but match no glob the profile configures.</summary>
/// <param name="Count">How many.</param>
/// <param name="Examples">Up to <see cref="RoleLikePaths.ExampleLimit"/> of their paths, in path order.</param>
public sealed record UnassignedRoleFiles(int Count, IReadOnlyList<string> Examples);
