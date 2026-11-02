namespace Aw.Context.Core.Extract;

/// <summary>The manifest, lock and build-config files by name: product code never lives in them, so a cluster feature is never keyed, named or implemented through one.</summary>
internal static class ManifestFiles
{
    private static readonly HashSet<string> _names = new(StringComparer.Ordinal)
    {
        "package.json", "package-lock.json", "npm-shrinkwrap.json", "yarn.lock", "pnpm-lock.yaml", "pnpm-workspace.yaml", "bun.lockb",
        "tsconfig.json", "jsconfig.json", "Cargo.toml", "Cargo.lock", "go.mod", "go.sum", "pyproject.toml", "poetry.lock",
        "Pipfile", "Pipfile.lock", "uv.lock", "Gemfile", "Gemfile.lock", "composer.json", "composer.lock", "packages.lock.json",
        "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json",
    };

    /// <summary>Whether the file's name (after the last '/') is a manifest, lock or build-config file.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <returns>True for a manifest file.</returns>
    internal static bool Is(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        return _names.Contains(name) || (name.StartsWith("tsconfig.", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal));
    }
}
