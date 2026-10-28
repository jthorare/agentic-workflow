
namespace Aw.Sindri.Core.Profile;

/// <summary>
/// Looks at the paths a repo tracks and suggests its 'sandbox.repos.&lt;repo&gt;' entries (sandbox spec §3.7): the host group or hosts, the cache paths and, for npm lock files, the prepare dirs of each toolchain it finds.
/// Pure: it takes a file listing, reads no disk, and writes nothing; the owner approves what it suggests with the profile.
/// Only names the profile already knows are suggested: the built-in group 'packages' (the nuget and npm hosts) for Node and dotnet, and explicit hosts for Python and Go, whose registries no built-in group holds.
/// </summary>
public static class SandboxToolchainDetector
{
    private const string LockFile = "package-lock.json";
    private const string PackageFile = "package.json";

    private static readonly string[] _packages = ["packages"];
    private static readonly string[] _pythonHosts = ["pypi.org", "files.pythonhosted.org"];
    private static readonly string[] _pythonCache = [".cache/pip"];
    private static readonly string[] _goHosts = ["proxy.golang.org", "sum.golang.org"];
    private static readonly string[] _goCache = ["go/pkg/mod", ".cache/go-build"];

    /// <summary>Detects the toolchains of a repo from its tracked paths.</summary>
    /// <param name="paths">The repo's tracked paths, relative to the repo and '/' separated.</param>
    /// <returns>Each toolchain found and the entries all of them add up to; no settings when none is found.</returns>
    public static ToolchainOffer Detect(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var files = paths.Where(static p => !p.Split('/').Contains("node_modules", StringComparer.Ordinal)).ToList();
        List<DetectedToolchain> found = [];
        AddIf(found, "Node", files.FirstOrDefault(static p => Named(p, PackageFile)), _ => new SandboxRepoSettings(Groups: _packages, Prepare: Prepare(files)));
        AddIf(found, "Python", files.FirstOrDefault(static p => Named(p, "pyproject.toml") || Named(p, "setup.py") || IsRequirements(p)), static _ => new SandboxRepoSettings(Hosts: _pythonHosts, CachePaths: _pythonCache));
        AddIf(found, "Go", files.FirstOrDefault(static p => Named(p, "go.mod")), static _ => new SandboxRepoSettings(Hosts: _goHosts, CachePaths: _goCache));
        AddIf(found, "dotnet", files.FirstOrDefault(static p => p.EndsWith(".csproj", StringComparison.Ordinal) || Named(p, "global.json")), static _ => new SandboxRepoSettings(Groups: _packages));
        return new ToolchainOffer(found, found.Count == 0 ? null : Merge(found.Select(static t => t.Entries)));
    }

    private static void AddIf(List<DetectedToolchain> found, string name, string? evidence, Func<string, SandboxRepoSettings> entries)
    {
        if (evidence is not null)
        {
            found.Add(new DetectedToolchain(name, evidence, entries(evidence)));
        }
    }

    // A directory is prepared when it holds both package.json and package-lock.json.
    private static SandboxPrepare? Prepare(List<string> files)
    {
        var dirs = files.Where(static p => Named(p, LockFile))
            .Select(static p => DirOf(p))
            .Where(dir => files.Contains(dir == "." ? PackageFile : dir + "/" + PackageFile, StringComparer.Ordinal))
            .Select(static dir => new PrepareDir(dir, "npm ci"))
            .ToList();
        return dirs.Count == 0 ? null : new SandboxPrepare(dirs);
    }

    private static SandboxRepoSettings Merge(IEnumerable<SandboxRepoSettings> all)
    {
        var settings = all.ToList();
        return new SandboxRepoSettings(
            Groups: Union(settings.Select(static s => s.Groups)),
            Hosts: Union(settings.Select(static s => s.Hosts)),
            CachePaths: Union(settings.Select(static s => s.CachePaths)),
            Prepare: settings.Select(static s => s.Prepare).FirstOrDefault(static p => p is not null));
    }

    private static List<string>? Union(IEnumerable<IReadOnlyList<string>?> lists)
    {
        var all = lists.SelectMany(static l => l ?? []).Distinct(StringComparer.Ordinal).ToList();
        return all.Count == 0 ? null : all;
    }

    private static string DirOf(string path) => path.LastIndexOf('/') is var slash and >= 0 ? path[..slash] : ".";

    private static bool Named(string path, string name) => string.Equals(path[(path.LastIndexOf('/') + 1)..], name, StringComparison.Ordinal);

    private static bool IsRequirements(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        return name.StartsWith("requirements", StringComparison.Ordinal) && name.EndsWith(".txt", StringComparison.Ordinal);
    }
}
