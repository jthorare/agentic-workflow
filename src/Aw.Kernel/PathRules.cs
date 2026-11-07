namespace Aw.Kernel;

/// <summary>Pure path rules shared by the profile checks of more than one package.</summary>
public static class PathRules
{
    /// <summary>What is wrong with <paramref name="path"/> as an absolute path, or null: it must start with '/' and have no '..' segment.</summary>
    /// <param name="path">The path as written (after any '~/' expansion).</param>
    /// <returns>The problem as a phrase for a key path to carry, or null when the path is fine.</returns>
    public static string? AbsolutePathProblem(string? path) =>
        string.IsNullOrWhiteSpace(path) || !path.StartsWith('/') || path.Split('/').Contains("..") ? "must be an absolute path with no '..'" : null;
}
