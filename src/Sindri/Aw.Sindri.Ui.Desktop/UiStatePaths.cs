using System.Security.Cryptography;
using System.Text;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// The app's own files on this machine (UI spec §12.3): a 'ui' directory in Sindri's home, the directory that holds
/// control.json (<see cref="ControlFilePath"/>). Owner-only: the directory is 0700 and every file in it 0600.
/// </summary>
/// <param name="Directory">The 'ui' directory.</param>
/// <param name="Preferences">preferences.json, the local preferences.</param>
/// <param name="InstanceSocket">The single instance's Unix domain socket (see <see cref="SocketIn"/>).</param>
/// <param name="InstanceLock">The file whose lock is the single instance's claim.</param>
internal sealed record UiStatePaths(string Directory, string Preferences, string InstanceSocket, string InstanceLock)
{
    /// <summary>The claim holder's process id (a later launch names it when the holder doesn't answer).</summary>
    internal string InstancePid => Path.Combine(Directory, "instance.pid");

    /// <summary>The longest socket path used as is: sun_path holds 104 bytes on macOS and 108 on Linux, with room to spare.</summary>
    internal const int MaxSocketPath = 100;

    /// <summary>The paths under the home <see cref="ControlFilePath"/> resolves.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="userHome">The user's home directory.</param>
    /// <returns>The paths.</returns>
    internal static UiStatePaths From(Func<string, string?> environment, string userHome) =>
        In(Path.Combine(Path.GetDirectoryName(ControlFilePath.From(environment, userHome))!, "ui"), Path.GetTempPath(), environment("XDG_RUNTIME_DIR")); // why: ControlFilePath always joins a directory and a file

    /// <summary>The paths in <paramref name="directory"/>, a long socket path falling back under the user's temp directory.</summary>
    /// <param name="directory">The 'ui' directory.</param>
    /// <returns>The paths.</returns>
    internal static UiStatePaths In(string directory) => In(directory, Path.GetTempPath());

    /// <summary>The paths in <paramref name="directory"/>.</summary>
    /// <param name="directory">The 'ui' directory.</param>
    /// <param name="tempRoot">Where a socket path too long for sun_path goes instead.</param>
    /// <param name="runtimeDir">XDG_RUNTIME_DIR, tried when <paramref name="tempRoot"/> is too long; null or empty when there is none.</param>
    /// <returns>The paths.</returns>
    internal static UiStatePaths In(string directory, string tempRoot, string? runtimeDir = null) => new(
        directory,
        Path.Combine(directory, "preferences.json"),
        SocketIn(Path.Combine(directory, "instance.sock"), tempRoot, runtimeDir),
        Path.Combine(directory, "instance.lock"));

    /// <summary>
    /// The socket's path: <paramref name="preferred"/> when it fits sun_path, else instance.sock in an owner-only directory
    /// under <paramref name="tempRoot"/> named by a hash of <paramref name="preferred"/>, so each home keeps its own channel.
    /// </summary>
    /// <param name="preferred">The socket's path in the 'ui' directory.</param>
    /// <param name="tempRoot">The user's temp directory.</param>
    /// <param name="runtimeDir">XDG_RUNTIME_DIR, or null or empty. When <paramref name="tempRoot"/> is too long too (an agent's scratchpad is 99 bytes), the first of it, <paramref name="runtimeDir"/> and /tmp whose path fits is used.</param>
    /// <returns>The path to bind.</returns>
    internal static string SocketIn(string preferred, string tempRoot, string? runtimeDir = null)
    {
        if (Encoding.UTF8.GetByteCount(preferred) <= MaxSocketPath)
        {
            return preferred;
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(preferred)))[..16];
        var roots = new[] { tempRoot, runtimeDir ?? "", "/tmp" }.Where(static r => r.Length > 0);
        var candidates = roots.Select(r => Path.Combine(r, "sindri-ui-" + hash, "instance.sock")).ToList();
        return candidates.FirstOrDefault(static c => Encoding.UTF8.GetByteCount(c) <= MaxSocketPath) ?? candidates[0];
    }
}
