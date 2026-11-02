namespace Aw.Context.Core.Extract.Imports;

/// <summary>Resolves a TS or JS module specifier, written in one file, to a file of the same repo.</summary>
internal sealed class TsResolver
{
    private static readonly string[] _extensions = [".ts", ".tsx", ".js", ".jsx", ".mjs"];
    private static readonly string[] _compiledExtensions = [".js", ".jsx", ".mjs"];

    private readonly HashSet<string> _paths;
    private readonly IReadOnlyList<PackageRoot> _packages;
    private readonly IReadOnlyList<PathAlias> _aliases;

    internal TsResolver(IReadOnlyList<SourceFile> files)
    {
        _paths = files.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        _packages = TsWorkspace.Packages(files);
        _aliases = TsWorkspace.Aliases(files);
    }

    internal string? Resolve(string from, string specifier)
    {
        if (string.Equals(specifier, ".", StringComparison.Ordinal) || string.Equals(specifier, "..", StringComparison.Ordinal) || specifier.StartsWith("./", StringComparison.Ordinal) || specifier.StartsWith("../", StringComparison.Ordinal))
        {
            return RepoPath.Join(RepoPath.Dir(from), specifier) is { } path ? File(path) : null;
        }

        return ResolvePackage(specifier) ?? ResolveAlias(from, specifier);
    }

    private string? ResolvePackage(string specifier)
    {
        foreach (var package in _packages)
        {
            if (specifier == package.Name)
            {
                return package.Entries.Select(File).FirstOrDefault(r => r is not null) ?? File(package.Dir + "/index") ?? File(package.Dir + "/src/index");
            }

            if (specifier.StartsWith(package.Name + "/", StringComparison.Ordinal))
            {
                var sub = specifier[(package.Name.Length + 1)..];
                return Under(package.Dir, sub) ?? Under(package.Dir + "/src", sub);
            }
        }

        return null;
    }

    private string? Under(string dir, string sub) => RepoPath.Join(dir, sub) is { } path ? File(path) : null;

    private string? ResolveAlias(string from, string specifier)
    {
        foreach (var alias in _aliases.Where(a => RepoPath.Within(a.Scope, from) && Matches(a, specifier)))
        {
            var middle = alias.Wild ? specifier[alias.Prefix.Length..^alias.Suffix.Length] : "";
            var hit = alias.Targets.Select(t => File(alias.Wild ? t.Replace("*", middle, StringComparison.Ordinal) : t)).FirstOrDefault(r => r is not null);
            if (hit is not null)
            {
                return hit;
            }
        }

        return null;
    }

    private static bool Matches(PathAlias alias, string specifier) =>
        alias.Wild
            ? specifier.Length >= alias.Prefix.Length + alias.Suffix.Length && specifier.StartsWith(alias.Prefix, StringComparison.Ordinal) && specifier.EndsWith(alias.Suffix, StringComparison.Ordinal)
            : specifier == alias.Prefix;

    /// <summary>The file a path names: itself, itself with an extension, a folder's index, or (for a compiled '.js' name) its TS source.</summary>
    private string? File(string path)
    {
        if (path.Length == 0)
        {
            return null;
        }

        if (_paths.Contains(path))
        {
            return path;
        }

        var stem = _compiledExtensions.FirstOrDefault(e => path.EndsWith(e, StringComparison.Ordinal)) is { } compiled ? path[..^compiled.Length] : null;
        var candidates = _extensions.Select(e => path + e).Concat(_extensions.Select(e => path + "/index" + e));
        if (stem is not null)
        {
            candidates = candidates.Concat([stem + ".ts", stem + ".tsx"]);
        }

        return candidates.FirstOrDefault(_paths.Contains);
    }
}
