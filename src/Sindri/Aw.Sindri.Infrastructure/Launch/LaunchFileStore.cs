using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The disk side of <see cref="ILaunchFiles"/> (API spec §8.1, launcher L-9): the secret hashes under <c lang="text">launch/secrets/</c> and the flat files under <c lang="text">launches/</c>.</summary>
/// <param name="options">Where the files live.</param>
internal sealed partial class LaunchFileStore(LauncherOptions options) : ILaunchFiles
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
        var removed = McpLaunchConfig.Sweep(options.LaunchDirectory, now, olderThan, live) + SweepSecrets(now, olderThan, live);
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

    [GeneratedRegex("^(?<launch>LCH-[0-9a-f]{32})\\.(?:capture\\.json|session-id)\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PerLaunch { get; }
}
