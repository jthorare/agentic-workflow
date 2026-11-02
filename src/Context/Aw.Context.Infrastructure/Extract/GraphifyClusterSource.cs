using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Infrastructure.Git;
using Aw.Kernel;
using Aw.Redaction;

namespace Aw.Context.Infrastructure.Extract;

/// <summary>
/// Clusters a repo's code with graphify, pinned and sandboxed (network denied, writes confined to a private snapshot of
/// the commit). Absent, wrong version, no sandbox or a failed run is an unavailable, empty set, never an error: the build
/// goes on with deterministic anchors and says 'degraded: clusters' with the reason (timed out, snapshot truncated, graph too large, graphify not found, no sandbox). graphify never runs unsandboxed.
/// </summary>
/// <param name="git">Exports the commit.</param>
/// <param name="options">The pin and binary.</param>
internal sealed partial class GraphifyClusterSource(GitCli git, GraphifyOptions options) : IClusterSource
{
    /// <summary>graphify's private temp and cache folder inside the snapshot (the only writable path); a repo's tracked files never land under it.</summary>
    internal const string PrivateFolder = ".sindri-tmp";

    private static readonly ClusterSet _none = new([], Available: false);

    public async ValueTask<Result<ClusterSet>> Clusters(ContextRepo repo, string commit, ClusterLimits limits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(limits);
        try
        {
            return await Cluster(repo, commit, limits, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Unavailable("an I/O error while preparing or reading the snapshot");   // a temp folder that can't be made, a disk that fills: the build goes on, degraded: clusters
        }
    }

    private static ClusterSet Unavailable(string reason) => _none with { Reason = reason };

    private async ValueTask<Result<ClusterSet>> Cluster(ContextRepo repo, string commit, ClusterLimits limits, CancellationToken cancellationToken)
    {
        var host = SandboxHost.Current();
        var (binary, problem) = await Check(host, cancellationToken).ConfigureAwait(false);
        if (binary is null || problem is not null)
        {
            return Unavailable(problem?.Short ?? "graphify not found");
        }

        var snapshot = Directory.CreateTempSubdirectory("aw-graphify-").FullName;
        try
        {
            var tally = new GraphifySnapshot.Tally(limits);
            var exported = await git.Archive(repo.Path, commit, (stream, token) => GraphifySnapshot.Unpack(stream, snapshot, repo, tally, token), cancellationToken).ConfigureAwait(false);
            var set = exported.IsError ? Unavailable("the commit could not be exported from git") : await Run(host, binary, snapshot, limits, cancellationToken).ConfigureAwait(false);
            return Truncated(set, tally, limits);
        }
        finally
        {
            DeleteQuietly(snapshot);
        }
    }

    // What the snapshot caps left out is part of the reason, so a build that clusters a partial repo says so.
    private static ClusterSet Truncated(ClusterSet set, GraphifySnapshot.Tally tally, ClusterLimits limits)
    {
        var notes = new List<string>();
        if (tally.OverSnapshot > 0)
        {
            notes.Add(string.Create(CultureInfo.InvariantCulture, $"snapshot truncated at {limits.MaxSnapshotMB} MB ({Files(tally.OverSnapshot)} skipped)"));
        }

        if (tally.OverFile > 0)
        {
            notes.Add(string.Create(CultureInfo.InvariantCulture, $"{Files(tally.OverFile)} over {limits.MaxFileMB} MB left out"));
        }

        return notes.Count == 0 ? set : set with { Reason = string.Join("; ", set.Reason is null ? notes : [set.Reason, .. notes]) };
    }

    private static string Files(int count) => count == 1 ? "1 file" : string.Create(CultureInfo.InvariantCulture, $"{count} files");

    private async ValueTask<ClusterSet> Run(SandboxHost host, string binary, string snapshot, ClusterLimits limits, CancellationToken cancellationToken)
    {
        var real = host.RealPath(snapshot);
        var priv = PrivateDirectory(real);
        _ = Directory.CreateDirectory(Path.Combine(priv, "cache"));
        var argv = options.Sandbox(host, [binary, "extract", real, "--code-only", "--no-viz"], [real]);
        if (argv is null)
        {
            return Unavailable("no sandbox");
        }

        var ran = await GraphifyProcess.Spawn(argv, PrivateEnvironment(priv), real, limits.TimeoutMinutes == 0 ? null : limits.TimeoutMinutes * options.MinuteLength, cancellationToken).ConfigureAwait(false);
        if (ran is not { TimedOut: false, Exit: 0 })
        {
            return Unavailable(ran switch
            {
                null => "graphify did not start",
                { TimedOut: true } => string.Create(CultureInfo.InvariantCulture, $"timed out after {limits.TimeoutMinutes} min"),
                _ => GraphifyFailure.Message(ran.Exit, ran.Stderr, static text => Redactor.Default.Scrub(text).Text),
            });
        }

        var (graph, why) = OpenGraph(real, limits);
        if (graph is null)
        {
            return Unavailable(why!);   // why: OpenGraph gives a reason whenever it gives no stream
        }

        await using (graph.ConfigureAwait(false))
        {
            var clusters = GraphifyGraph.Clusters(graph, [snapshot, real]);
            return clusters.IsError ? Unavailable("graph.json could not be read") : new ClusterSet(clusters.Value, Available: true);
        }
    }

    // graphify's private folder, made inside the snapshot (the only path it may write) and readable by this user alone. CreateTempSubdirectory only takes a name prefix and always lands in the system temp folder.
    private static string PrivateDirectory(string snapshot)
    {
        var path = Path.Combine(snapshot, PrivateFolder + "-" + Path.GetRandomFileName());
        _ = OperatingSystem.IsWindows()
            ? Directory.CreateDirectory(path)
            : Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    /// <summary>Null when this machine can run the pinned graphify under the sandbox; else why it can't (no sandbox, no binary, a sandbox that won't start, the wrong version). The same check, sandbox argv, timeout and kill the build uses.</summary>
    /// <param name="cancellationToken">Stops the probe.</param>
    internal async ValueTask<string?> WhyUnavailable(CancellationToken cancellationToken) =>
        (await Check(SandboxHost.Current(), cancellationToken).ConfigureAwait(false)).Problem?.Why;

    // The binary's path (null when absent) and the problem that keeps it from being used (null when usable): --version run inside the sandbox must print the pin.
    private async ValueTask<(string? Binary, Unusable? Problem)> Check(SandboxHost host, CancellationToken cancellationToken)
    {
        if (Find(options.Binary) is not { } binary)
        {
            return (null, new Unusable($"no {options.Binary} binary found", "graphify not found"));
        }

        var scratch = Directory.CreateTempSubdirectory("aw-graphify-probe-").FullName;
        try
        {
            var argv = options.Sandbox(host, [binary, "--version"], []);
            if (argv is null)
            {
                return (binary, new Unusable("no sandbox available on this machine", "no sandbox"));
            }

            var ran = await GraphifyProcess.Spawn(argv, PrivateEnvironment(scratch), scratch, TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            if (ran is not { Exit: 0 })
            {
                return (binary, new Unusable("the sandboxed graphify --version did not start, failed or timed out", "graphify did not start under the sandbox"));
            }

            return Version.Match(ran.Stdout) is { Success: true } found && string.Equals(found.Value, options.Pin, StringComparison.Ordinal)
                ? (binary, null)
                : (binary, new Unusable($"graphify is not the pin {options.Pin}", $"graphify is not the pin {options.Pin}"));
        }
        finally
        {
            DeleteQuietly(scratch);
        }
    }

    private static Dictionary<string, string> PrivateEnvironment(string priv) => new(StringComparer.Ordinal)
    {
        ["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin:/bin",
        ["LANG"] = "C.UTF-8",
        ["HOME"] = priv,
        ["TMPDIR"] = priv,
        ["XDG_CACHE_HOME"] = Path.Combine(priv, "cache"),
        ["UV_CACHE_DIR"] = Path.Combine(priv, "cache", "uv"),
        ["PYTHONPYCACHEPREFIX"] = Path.Combine(priv, "pycache"),
    };

    // The binary's absolute path: as given, or the first match on PATH.
    private static string? Find(string binary)
    {
        if (Path.IsPathRooted(binary))
        {
            return File.Exists(binary) ? binary : null;
        }

        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => Path.Combine(dir, binary)).FirstOrDefault(File.Exists);
    }

    // graph.json is the sandboxed tool's output and is read unsandboxed: never through a link, only a regular file, never over the cap. Opened for streaming (no whole-file string), or why not.
    private static (Stream? Graph, string? Why) OpenGraph(string snapshot, ClusterLimits limits)
    {
        try
        {
            var folder = new DirectoryInfo(Path.Combine(snapshot, "graphify-out"));
            var file = new FileInfo(Path.Combine(folder.FullName, "graph.json"));
            if (!folder.Exists || folder.LinkTarget is not null || !file.Exists || file.LinkTarget is not null)
            {
                return (null, "graphify wrote no graph.json");
            }

            return limits.MaxGraphBytes is { } cap && file.Length > cap
                ? (null, string.Create(CultureInfo.InvariantCulture, $"graph.json over {limits.MaxGraphMB} MB"))
                : (new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read), null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (null, "graph.json could not be read");
        }
    }

    private static void DeleteQuietly(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // a temp folder that stays is harmless; the next run makes its own
        }
    }

    [GeneratedRegex("[0-9]+\\.[0-9]+\\.[0-9]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Version { get; }

    private sealed record Unusable(string Why, string Short);
}
