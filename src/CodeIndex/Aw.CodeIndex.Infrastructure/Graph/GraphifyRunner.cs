using System.Globalization;
using System.Text.RegularExpressions;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;

namespace Aw.CodeIndex.Infrastructure.Graph;

/// <summary>
/// Runs the pinned graphify on a snapshot of a commit (spec §5.5): only under a sandbox that denies the network and confines writes to the snapshot and
/// its private folder, the version checked against the pin before every run, graph.json read as hostile input. Anything wrong is a layer code
/// (007 no sandbox, 008 missing or failed, 009 pin), never an exception; the build goes on without the layer.
/// </summary>
/// <param name="options">Where graphify is and how long it may run.</param>
/// <param name="scrubber">Scrubs and cuts the node names.</param>
internal sealed partial class GraphifyRunner(GraphifyOptions options, IBodyScrubber scrubber) : IGraphRunner
{
    private static readonly TimeSpan _probeLimit = TimeSpan.FromSeconds(30);

    private string? _checkedKey;

    /// <inheritdoc />
    public async ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken)
    {
        var binary = options.Binary;
        if (!File.Exists(binary))
        {
            return CodeIndexErrors.GraphUnavailable.With("missing");
        }

        var info = new FileInfo(binary);
        if (info.LinkTarget is not null)
        {
            return CodeIndexErrors.PinMismatch.With("link");
        }

        var key = string.Create(CultureInfo.InvariantCulture, $"{binary}|{info.LastWriteTimeUtc.Ticks}|{info.Length}");
        if (string.Equals(_checkedKey, key, StringComparison.Ordinal))
        {
            return Unit.Value;
        }

        var scratch = NewDirectory("probe");
        try
        {
            var checkedVersion = await CheckVersion(scratch, cancellationToken).ConfigureAwait(false);
            _checkedKey = checkedVersion.IsError ? null : key;
            return checkedVersion;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return CodeIndexErrors.GraphUnavailable.With("failed");
        }
        finally
        {
            DeleteQuietly(scratch);
        }
    }

    // --version run under the same wrapper as the extraction, so graphify never runs outside the sandbox, not even to name its version.
    private async ValueTask<Result<Unit>> CheckVersion(string scratch, CancellationToken cancellationToken)
    {
        var argv = options.Sandbox(options.Host, [options.Binary, "--version"], [options.Host.RealPath(scratch)], [EnvironmentOf(options.Binary)]);
        if (argv is null)
        {
            return CodeIndexErrors.NoSandbox.With("none");
        }

        var ran = await GraphProcess.Spawn(argv, PrivateEnvironment(scratch), scratch, _probeLimit, cancellationToken).ConfigureAwait(false);
        if (ran is not { Exit: 0, TimedOut: false })
        {
            return CodeIndexErrors.GraphUnavailable.With("no-start");
        }

        return Version.Match(ran.Stdout) is { Success: true } found && string.Equals(found.Value, options.Pin, StringComparison.Ordinal)
            ? Unit.Value
            : CodeIndexErrors.PinMismatch.With("version");
    }

    /// <inheritdoc />
    public async ValueTask<Result<GraphSnapshot>> Run(GraphRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var probe = await Probe(cancellationToken).ConfigureAwait(false);
        if (probe.IsError)
        {
            return probe.Error;
        }

        var run = NewDirectory("run");
        try
        {
            return await RunIn(request, run, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return CodeIndexErrors.GraphUnavailable.With("failed");
        }
        finally
        {
            DeleteQuietly(run);
        }
    }

    private async Task<Result<GraphSnapshot>> RunIn(GraphRequest request, string run, CancellationToken cancellationToken)
    {
        var snapshot = Directory.CreateDirectory(Path.Combine(run, "snapshot")).FullName;
        var priv = Directory.CreateDirectory(Path.Combine(run, "private")).FullName;
        _ = Directory.CreateDirectory(Path.Combine(priv, "cache"));
        var written = await GraphSnapshotWriter.Write(request, snapshot, options, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return CodeIndexErrors.GraphUnavailable.With("failed");
        }

        var host = options.Host;
        var (realSnapshot, realPrivate) = (host.RealPath(snapshot), host.RealPath(priv));
        var argv = options.Sandbox(host, [options.Binary, "extract", realSnapshot, "--code-only", "--no-viz"], [realSnapshot, realPrivate], [EnvironmentOf(options.Binary)]);
        if (argv is null)
        {
            return CodeIndexErrors.NoSandbox.With("none");
        }

        var ran = await GraphProcess.Spawn(argv, PrivateEnvironment(realPrivate), realSnapshot, options.Timeout, cancellationToken).ConfigureAwait(false);
        if (ran is null)
        {
            return CodeIndexErrors.GraphUnavailable.With("no-start");
        }

        if (ran.TimedOut || ran.Exit != 0)
        {
            return CodeIndexErrors.GraphUnavailable.With(ran.TimedOut ? "timeout" : "failed");
        }

        var json = await ReadGraph(snapshot, request.Settings, cancellationToken).ConfigureAwait(false);
        if (json.IsError)
        {
            return json.Error;
        }

        var files = request.Files.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        return GraphJsonParser.Parse(json.Value, files, [snapshot, realSnapshot], request.Settings.MaxNodes, scrubber.Scrub);
    }

    // graph.json is the sandboxed tool's output, read unsandboxed: a regular file through no link, never over the cap.
    private static async Task<Result<byte[]>> ReadGraph(string snapshot, GraphSettings settings, CancellationToken cancellationToken)
    {
        var folder = new DirectoryInfo(Path.Combine(snapshot, "graphify-out"));
        var file = new FileInfo(Path.Combine(folder.FullName, "graph.json"));
        if (!folder.Exists || folder.LinkTarget is not null || !file.Exists || file.LinkTarget is not null)
        {
            return CodeIndexErrors.GraphUnavailable.With("shape");
        }

        return file.Length > (long)settings.MaxMegabytes * 1024 * 1024
            ? CodeIndexErrors.GraphUnavailable.With("too-large")
            : await File.ReadAllBytesAsync(file.FullName, cancellationToken).ConfigureAwait(false);
    }

    // The pinned environment: the binary's folder is <env>/bin, so the environment is its parent; bound read-only after the credential masks.
    private string EnvironmentOf(string binary)
    {
        var bin = Path.GetDirectoryName(Path.GetFullPath(binary))!;   // why: a file path has a directory
        return options.Host.RealPath(Path.GetDirectoryName(bin) ?? bin);
    }

    // A fresh parent made with an exclusive mkdir at 0700 under the code-index temp root, never reused.
    private string NewDirectory(string kind)
    {
        PrivateDirectory(options.TempRoot);
        var path = Path.Combine(options.TempRoot, string.Create(CultureInfo.InvariantCulture, $"{kind}-{Environment.ProcessId}-{Random.Shared.NextInt64():x}"));
        PrivateDirectory(path);
        return path;
    }

    private static void PrivateDirectory(string path)
    {
        _ = OperatingSystem.IsWindows()
            ? Directory.CreateDirectory(path)
            : Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static Dictionary<string, string> PrivateEnvironment(string priv) => new(StringComparer.Ordinal)
    {
        ["PATH"] = "/usr/bin:/bin",
        ["LANG"] = "C.UTF-8",
        ["HOME"] = priv,
        ["TMPDIR"] = priv,
        ["XDG_CACHE_HOME"] = Path.Combine(priv, "cache"),
        ["UV_CACHE_DIR"] = Path.Combine(priv, "cache", "uv"),
        ["PYTHONPYCACHEPREFIX"] = Path.Combine(priv, "pycache"),
    };

    private static void DeleteQuietly(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // a folder that stays is harmless; the next run makes its own
        }
    }

    [GeneratedRegex("[0-9]+\\.[0-9]+\\.[0-9]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Version { get; }
}
