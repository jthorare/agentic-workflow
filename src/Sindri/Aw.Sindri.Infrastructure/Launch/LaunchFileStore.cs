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
        catch (IOException)
        {
            return refused;
        }
    }

    public void Forget(LaunchId launch)
    {
        LaunchSecrets.Delete(options.SecretsDirectory, launch);
        DeleteFlat(options, launch);
    }

    public IReadOnlyList<LaunchId> Launches()
    {
        if (!Directory.Exists(options.LaunchesDirectory))
        {
            return [];
        }

        var found = new HashSet<LaunchId>();
        foreach (var file in Directory.EnumerateFiles(options.LaunchesDirectory))
        {
            var match = PerLaunch.Match(Path.GetFileName(file));
            if (match.Success && LaunchId.TryParse(match.Groups["launch"].Value, provider: null, out var launch))
            {
                _ = found.Add(launch);
            }
        }

        return [.. found];
    }

    public int SweepStale(DateTimeOffset now, TimeSpan olderThan, Func<LaunchId, bool> live)
    {
        ArgumentNullException.ThrowIfNull(live);
        if (!Directory.Exists(options.LaunchesDirectory))
        {
            return 0;
        }

        var removed = 0;
        foreach (var file in Directory.EnumerateFiles(options.LaunchesDirectory))
        {
            var match = PerLaunch.Match(Path.GetFileName(file));
            if (match.Success && LaunchId.TryParse(match.Groups["launch"].Value, provider: null, out var launch)
                && now - new DateTimeOffset(File.GetLastWriteTimeUtc(file)) > olderThan && !live(launch) && TryDelete(file))
            {
                removed++;
            }
        }

        return removed;
    }

    /// <summary>Deletes the launch's capture rule and reported-id file; whatever is gone is fine.</summary>
    /// <param name="options">Where the files live.</param>
    /// <param name="launch">The launch.</param>
    internal static void DeleteFlat(LauncherOptions options, LaunchId launch)
    {
        _ = TryDelete(Path.Combine(options.LaunchesDirectory, Core.Launch.LaunchFiles.CaptureName(launch)));
        _ = TryDelete(Path.Combine(options.LaunchesDirectory, Core.Launch.LaunchFiles.SessionIdName(launch)));
    }

    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    [GeneratedRegex("^(?<launch>LCH-[0-9a-f]{32})\\.(?:capture\\.json|session-id)\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PerLaunch { get; }
}
