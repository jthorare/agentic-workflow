using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Roles;

/// <summary>The paths that look like a file role (tests, fixtures, mocks) whether or not the profile gives them one.</summary>
public static class RoleLikePaths
{
    /// <summary>How many example paths an unassigned report keeps.</summary>
    public const int ExampleLimit = 5;

    /// <summary>The globs of role-like paths, in <see cref="PathDenylist"/> syntax.</summary>
    public static IReadOnlyList<string> Patterns { get; } =
        ["**/__tests__/**", "**/*.test.*", "**/*.spec.*", "**/fixtures/**", "**/__mocks__/**", "**/testdata/**"];

    /// <summary>Whether a path looks like a role.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <returns>True when a pattern matches it.</returns>
    public static bool Matches(string path) => PathDenylist.Matches(path, Patterns);
}
