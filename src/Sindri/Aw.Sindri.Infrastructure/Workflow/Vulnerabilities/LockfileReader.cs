#pragma warning disable MA0182 // why: the graph block registry is not registered in DI yet (EmptyGraphBlockCatalog); remove this when it is
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Workflow.Vulnerabilities;

/// <summary>Reads packages.lock.json, package-lock.json, go.sum, Cargo.lock and poetry.lock under a directory.</summary>
internal static class LockfileReader
{
    private static readonly string[] _skipped = ["node_modules", ".git", "bin", "obj"];
    private static readonly string[] _lockfiles = ["packages.lock.json", "package-lock.json", "go.sum", "Cargo.lock", "poetry.lock"];

    /// <summary>Reads every supported lockfile in <paramref name="directory"/> (its subfolders too, except node_modules, .git, bin and obj).</summary>
    /// <param name="directory">The repo's checkout.</param>
    /// <returns>The packages, once each; or why the directory could not be read.</returns>
    public static Result<IReadOnlyList<VulnPackage>> ReadDirectory(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        if (!Directory.Exists(directory))
        {
            return SindriErrors.GraphBlockFailed.With($"'{directory}' is not a folder; there are no lockfiles to read");
        }

        var found = new List<VulnPackage>();
        try
        {
            foreach (var file in Walk(directory))
            {
                found.AddRange(Read(file));
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return SindriErrors.GraphBlockFailed.With($"a lockfile could not be read ({ex.Message})");
        }

        return found.Distinct().ToList();
    }

    private static IEnumerable<string> Walk(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory).Where(static f => _lockfiles.Contains(Path.GetFileName(f), StringComparer.Ordinal)))
        {
            yield return file;
        }

        foreach (var sub in Directory.EnumerateDirectories(directory).Where(static d => !_skipped.Contains(Path.GetFileName(d), StringComparer.Ordinal)))
        {
            foreach (var file in Walk(sub))
            {
                yield return file;
            }
        }
    }

    private static List<VulnPackage> Read(string file)
    {
        var text = File.ReadAllText(file);
        return Path.GetFileName(file) switch
        {
            "packages.lock.json" => NuGet(text),
            "package-lock.json" => Npm(text),
            "go.sum" => GoSum(text),
            "Cargo.lock" => TomlPackages.Read(text, "crates.io", requireSource: true),
            _ => TomlPackages.Read(text, "PyPI", requireSource: false),
        };
    }

    private static List<VulnPackage> NuGet(string text)
    {
        using var doc = JsonDocument.Parse(text);
        var found = new List<VulnPackage>();
        if (!doc.RootElement.TryGetProperty("dependencies", out var frameworks))
        {
            return found;
        }

        foreach (var dep in frameworks.EnumerateObject().SelectMany(static f => f.Value.EnumerateObject()))
        {
            var isProject = dep.Value.TryGetProperty("type", out var type) && string.Equals(type.GetString(), "Project", StringComparison.Ordinal);
            if (!isProject && dep.Value.TryGetProperty("resolved", out var resolved) && resolved.GetString() is { } version)
            {
                found.Add(new VulnPackage("NuGet", dep.Name, version));
            }
        }

        return found;
    }

    private static List<VulnPackage> Npm(string text)
    {
        using var doc = JsonDocument.Parse(text);
        var found = new List<VulnPackage>();
        if (doc.RootElement.TryGetProperty("packages", out var packages))
        {
            const string Marker = "node_modules/";
            foreach (var entry in packages.EnumerateObject().Where(static e => e.Name.Length > 0))
            {
                var at = entry.Name.LastIndexOf(Marker, StringComparison.Ordinal);
                if (at >= 0 && entry.Value.TryGetProperty("version", out var v) && v.GetString() is { } version)
                {
                    found.Add(new VulnPackage("npm", entry.Name[(at + Marker.Length)..], version));
                }
            }
        }
        else if (doc.RootElement.TryGetProperty("dependencies", out var legacy))
        {
            Legacy(legacy, found);
        }

        return found;
    }

    private static void Legacy(JsonElement dependencies, List<VulnPackage> found)
    {
        foreach (var dep in dependencies.EnumerateObject())
        {
            if (dep.Value.TryGetProperty("version", out var v) && v.GetString() is { } version)
            {
                found.Add(new VulnPackage("npm", dep.Name, version));
            }

            if (dep.Value.TryGetProperty("dependencies", out var nested))
            {
                Legacy(nested, found);
            }
        }
    }

    private static List<VulnPackage> GoSum(string text)
    {
        var found = new List<VulnPackage>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && !parts[1].EndsWith("/go.mod", StringComparison.Ordinal))
            {
                found.Add(new VulnPackage("Go", parts[0], parts[1].TrimStart('v')));
            }
        }

        return found;
    }
}
