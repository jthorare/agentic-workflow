using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The disk side of <see cref="ILaunchFiles"/> (API spec §8.1, launcher L-9): the secret hashes under <c lang="text">launch/secrets/</c> and the flat files under <c lang="text">launches/</c>.</summary>
/// <param name="options">Where the files live.</param>
/// <param name="seams">What the sweep of a dead launch's sandbox files asks of the box (SB-9c); null is this box.</param>
internal sealed partial class LaunchFileStore(LauncherOptions options, StaleSweepSeams? seams = null) : ILaunchFiles
{
    public Result<Unit> Verify(LaunchId launch, LaunchSecret secret)
    {
        var refused = SindriErrors.AuthLaunchSecret.With("the launch secret doesn't match an open launch");
        if (string.IsNullOrEmpty(secret.Value))
        {
            return refused;
        }

        try
        {
            var path = LaunchSecrets.PathFor(options.SecretsDirectory, launch);
            if (!File.Exists(path))
            {
                return refused;
            }

            var stored = Encoding.UTF8.GetBytes(File.ReadAllText(path).Trim());
            return CryptographicOperations.FixedTimeEquals(stored, Encoding.UTF8.GetBytes(LaunchSecrets.Hash(secret.Value))) ? Unit.Value : refused;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return refused;
        }
    }

    public DateTimeOffset? TurnEndedAt(LaunchId launch) => MarkAt(Core.Launch.LaunchFiles.TurnEndedName(launch));

    public DateTimeOffset? TurnFailedAt(LaunchId launch) => MarkAt(Core.Launch.LaunchFiles.TurnFailedName(launch));

    public bool ToolRunning(LaunchId launch) => MarkAt(Core.Launch.LaunchFiles.ToolRunningName(launch)) is not null;

    public DateTimeOffset? NudgedAt(LaunchId launch) => MarkAt(Core.Launch.LaunchFiles.NudgedName(launch));

    public void MarkNudged(LaunchId launch, DateTimeOffset at)
    {
        var path = Path.Combine(options.LaunchesDirectory, Core.Launch.LaunchFiles.NudgedName(launch));
        try
        {
            OwnerOnly.EnsureDirectory(options.LaunchesDirectory);
            OwnerOnly.WriteAtomically(path, []);
            File.SetLastWriteTimeUtc(path, at.UtcDateTime);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // boundary: a marker that can't be written leaves the session as it was; the sweep tries again
        }
    }

    // When the hook-touched file was last written; null when it is gone or can't be read (a file that can't be read says nothing about the turn).
    private DateTimeOffset? MarkAt(string name)
    {
        var path = Path.Combine(options.LaunchesDirectory, name);
        try
        {
            return File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path)) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Forget(LaunchId launch)
    {
        LaunchSecrets.Delete(options.SecretsDirectory, launch);
        DeleteFlat(options, launch);
        if (Directory.Exists(options.LaunchDirectory))
        {
            McpLaunchConfig.Delete(options.LaunchDirectory, launch);
        }
    }

    public IReadOnlyList<LaunchId> Launches()
    {
        var found = new HashSet<LaunchId>();
        if (Directory.Exists(options.LaunchesDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(options.LaunchesDirectory))
            {
                var match = PerLaunch.Match(Path.GetFileName(file));
                if (match.Success && LaunchId.TryParse(match.Groups["launch"].Value, provider: null, out var launch))
                {
                    _ = found.Add(launch);
                }
            }
        }

        AddNamed(found, options.SecretsDirectory, "*.hash");
        AddNamed(found, options.SecretsDirectory, "*.secret");
        AddNamed(found, options.PolicyDirectory, "LCH-*.settings.json");
        return [.. found];
    }

    private static void AddNamed(HashSet<LaunchId> found, string directory, string pattern)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory, pattern))
        {
            var name = Path.GetFileName(file);
            if (LaunchId.TryParse(name[..name.IndexOf('.', StringComparison.Ordinal)], provider: null, out var launch))
            {
                _ = found.Add(launch);
            }
        }
    }

    public int SweepStale(DateTimeOffset now, TimeSpan olderThan, Func<LaunchId, bool> live)
    {
        ArgumentNullException.ThrowIfNull(live);
        var removed = McpLaunchConfig.Sweep(options.LaunchDirectory, now, olderThan, live) + SweepSecrets(now, olderThan, live) + SweepSessionHomes(now, olderThan, live) + SweepSandboxFiles(now, olderThan, live);
        if (!Directory.Exists(options.LaunchesDirectory))
        {
            return removed;
        }

        foreach (var file in Directory.EnumerateFiles(options.LaunchesDirectory))
        {
            var match = PerLaunch.Match(Path.GetFileName(file));
            if (match.Success && LaunchId.TryParse(match.Groups["launch"].Value, provider: null, out var launch)
                && now - new DateTimeOffset(File.GetLastWriteTimeUtc(file)) > olderThan && !live(launch) && OwnerOnly.TryDelete(file))
            {
                removed++;
            }
        }

        return removed;
    }

    /// <summary>
    /// SB-9c: the '.pgid' files of dead groups (any OS) and, on macOS, the 'aw-&lt;8 hex&gt;' temp folders of launches that are not live. A launch counts as live when
    /// it has a live session; the folder name carries only 8 hex digits of the id, so live launches are matched by it, from the launches that have files here.
    /// </summary>
    private int SweepSandboxFiles(DateTimeOffset now, TimeSpan olderThan, Func<LaunchId, bool> live)
    {
        var groups = ProcessGroupFiles.Directory(options.LaunchDirectory);
        var sweep = seams ?? new StaleSweepSeams();
        var removed = Directory.Exists(groups) ? StaleSandboxFiles.SweepGroupFiles(groups, live, sweep.Alive ?? ProcessGroupFiles.Alive) : 0;
        if (!sweep.Mac)
        {
            return removed;
        }

        // why: SweepSessionHomes ran first, so a session home that is still here is a live or just-prepared launch's, whether or not it has a launch file or a Live view row yet
        var liveFolders = Launches().Where(live).Concat(SessionHomeLaunches()).Select(l => Path.GetFileName(SandboxPaths.SeatbeltTemp(l))).ToHashSet(StringComparer.Ordinal);
        return removed + StaleSandboxFiles.SweepSeatbeltTemp(sweep.TempRoot, liveFolders, sweep.Owned ?? MacTempOwner.Owns, now, olderThan);
    }

    /// <summary>The launches that have a session home: a live Seatbelt launch has one until its release, before any capture or session-id file exists.</summary>
    public IReadOnlyList<LaunchId> SessionHomeLaunches()
    {
        if (options.SindriHome is not { } home || !Directory.Exists(SessionHomePaths.Sessions(home)))
        {
            return [];
        }

        return [.. Directory.EnumerateDirectories(SessionHomePaths.Sessions(home)).Select(d => LaunchId.TryParse(Path.GetFileName(d), provider: null, out var launch) ? (LaunchId?)launch : null).OfType<LaunchId>()];
    }

    /// <summary>Removes the session homes (sandbox spec §3.1) of launches that are not live and older than the limit: a launch just prepared has no live view row yet.</summary>
    private int SweepSessionHomes(DateTimeOffset now, TimeSpan olderThan, Func<LaunchId, bool> live) =>
        options.SindriHome is { } home
            ? SessionHome.Sweep(home, id => (LaunchId.TryParse(id, provider: null, out var launch) && live(launch))
                || now - new DateTimeOffset(Directory.GetLastWriteTimeUtc(Path.Combine(SessionHomePaths.Sessions(home), id))) <= olderThan).Count
            : 0;

    /// <summary>Deletes secret files that no wrap read (it never ran): older than the limit and not of a live launch.</summary>
    private int SweepSecrets(DateTimeOffset now, TimeSpan olderThan, Func<LaunchId, bool> live) =>
        SweepOwned(options.SecretsDirectory, "*.secret", now, olderThan, live)
        + SweepOwned(options.SecretsDirectory, "*.hash", now, olderThan, live)
        + SweepOwned(options.PolicyDirectory, "LCH-*.settings.json", now, olderThan, live);

    private static int SweepOwned(string directory, string pattern, DateTimeOffset now, TimeSpan olderThan, Func<LaunchId, bool> live)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var removed = 0;
        foreach (var file in Directory.EnumerateFiles(directory, pattern))
        {
            var name = Path.GetFileName(file);
            if (LaunchId.TryParse(name[..name.IndexOf('.', StringComparison.Ordinal)], provider: null, out var launch)
                && now - new DateTimeOffset(File.GetLastWriteTimeUtc(file)) > olderThan && !live(launch) && OwnerOnly.TryDelete(file))
            {
                removed++;
            }
        }

        return removed;
    }

    internal static void DeleteFlat(LauncherOptions options, LaunchId launch)
    {
        _ = OwnerOnly.TryDelete(Path.Combine(options.LaunchesDirectory, Core.Launch.LaunchFiles.CaptureName(launch)));
        _ = OwnerOnly.TryDelete(Path.Combine(options.LaunchesDirectory, Core.Launch.LaunchFiles.SessionIdName(launch)));
        _ = OwnerOnly.TryDelete(Path.Combine(options.LaunchesDirectory, Core.Launch.LaunchFiles.TurnEndedName(launch)));
        _ = OwnerOnly.TryDelete(Path.Combine(options.LaunchesDirectory, Core.Launch.LaunchFiles.TurnFailedName(launch)));
        _ = OwnerOnly.TryDelete(Path.Combine(options.LaunchesDirectory, Core.Launch.LaunchFiles.ToolRunningName(launch)));
        _ = OwnerOnly.TryDelete(Path.Combine(options.LaunchesDirectory, Core.Launch.LaunchFiles.NudgedName(launch)));
    }

    /// <summary>Deletes <paramref name="file"/> when its <paramref name="directory"/> exists; whatever is gone is fine.</summary>
    /// <param name="directory">The file's directory.</param>
    /// <param name="file">The file.</param>
    internal static void DeleteIn(string directory, string file)
    {
        if (Directory.Exists(directory))
        {
            _ = OwnerOnly.TryDelete(file);
        }
    }

    [GeneratedRegex("^(?<launch>LCH-[0-9a-f]{32})\\.(?:capture\\.json|session-id|turn-ended|turn-failed|tool-running|nudged)\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PerLaunch { get; }
}
