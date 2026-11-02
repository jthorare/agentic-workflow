using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// A launch's session home (sandbox spec §3.1, rev 7): <c lang="text">&lt;real home&gt;-sessions/&lt;launch&gt;/</c>, 0700, beside the real home. It holds the links the CLI and the hooks follow
/// (<c lang="text">launches</c>, <c lang="text">approved</c>), the control.json that names Sindri, the launch's one-read secret file and an empty <c lang="text">gh-config/</c>. The launcher makes it
/// before the sandbox and removes it at run close and in the crash sweep. The Step's policy file and MCP config are written into it by their own writers.
/// </summary>
internal static class SessionHome
{
    /// <summary>The name of the launch's one-read secret file inside the session home.</summary>
    public const string SecretName = "launch.secret";

    /// <summary>The empty GitHub CLI config folder's name inside the session home.</summary>
    public const string GhConfigName = "gh-config";

    private const UnixFileMode DirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    /// <summary>Gets where the launch's one-read secret file is (AW_SINDRI_LAUNCH_SECRET_FILE).</summary>
    /// <param name="sessionHome">The session home.</param>
    /// <returns>The path.</returns>
    public static string SecretFile(string sessionHome) => $"{sessionHome}/{SecretName}";

    /// <summary>
    /// Makes the launch's session home. Under the sandbox <paramref name="control"/> is the endpoint record to write (the real Url, pid and epoch; the inside forwarder listens on the same port),
    /// because the real file is not bound and a single-file bind would pin an old inode after a restart. Outside it (<paramref name="control"/> null) control.json is a link to the real file.
    /// An existing home of the same launch is replaced, so a retried launch starts clean.
    /// </summary>
    /// <param name="sindriHome">The real AW_SINDRI_HOME.</param>
    /// <param name="launch">The launch.</param>
    /// <param name="secret">The launch secret, written 0600 to <see cref="SecretName"/>.</param>
    /// <param name="control">The endpoint record to write as control.json, or null for a link to the real file.</param>
    /// <returns>The session home's path.</returns>
    public static string Create(string sindriHome, LaunchId launch, string secret, ControlEndpoint? control)
    {
        ArgumentNullException.ThrowIfNull(sindriHome);
        ArgumentNullException.ThrowIfNull(secret);
        var path = SessionHomePaths.For(sindriHome, launch);
        Remove(path);
        OwnerOnly.EnsureDirectory(SessionHomePaths.Sessions(sindriHome));
        _ = Directory.CreateDirectory(path);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, DirectoryMode);
        }

        _ = Directory.CreateSymbolicLink($"{path}/launches", $"{sindriHome}/launches");
        _ = Directory.CreateSymbolicLink($"{path}/approved", $"{sindriHome}/approved");
        OwnerOnly.EnsureDirectory($"{path}/{GhConfigName}");
        OwnerOnly.WriteAtomically(SecretFile(path), System.Text.Encoding.UTF8.GetBytes(secret));
        if (control is null)
        {
            _ = File.CreateSymbolicLink($"{path}/control.json", $"{sindriHome}/control.json");
        }
        else
        {
            WriteControl(path, control);
        }

        return path;
    }

    /// <summary>Writes control.json into the session home by rename, so a reader never sees half a file (<c lang="text">session wrap</c> calls it again when serve restarts and the epoch changes).</summary>
    /// <param name="sessionHome">The session home.</param>
    /// <param name="control">The endpoint record.</param>
    public static void WriteControl(string sessionHome, ControlEndpoint control)
    {
        ArgumentNullException.ThrowIfNull(control);
        OwnerOnly.WriteAtomically($"{sessionHome}/control.json", Kernel.Json.AwJson.Serialize(control, SandboxFilesJson.Options));
    }

    /// <summary>Removes a session home. The links go, never what they point at; a missing home is fine.</summary>
    /// <param name="sessionHome">The session home.</param>
    public static void Remove(string sessionHome)
    {
        ArgumentNullException.ThrowIfNull(sessionHome);
        try
        {
            Directory.Delete(sessionHome, recursive: true);   // .NET removes a symbolic link inside, it does not follow it
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // a home that can't be removed now is taken by the next sweep
        }
    }

    /// <summary>Removes the session homes of launches that are no longer live (the crash sweep: at serve start and on Check again).</summary>
    /// <param name="sindriHome">The real AW_SINDRI_HOME.</param>
    /// <param name="isLive">Says whether a launch id still has a live session.</param>
    /// <returns>The launches whose homes were removed.</returns>
    public static IReadOnlyList<string> Sweep(string sindriHome, Func<string, bool> isLive)
    {
        ArgumentNullException.ThrowIfNull(sindriHome);
        ArgumentNullException.ThrowIfNull(isLive);
        var sessions = SessionHomePaths.Sessions(sindriHome);
        if (!Directory.Exists(sessions))
        {
            return [];
        }

        var removed = new List<string>();
        foreach (var directory in Directory.EnumerateDirectories(sessions))
        {
            var launch = Path.GetFileName(directory);
            if (isLive(launch))
            {
                continue;
            }

            Remove(directory);
            if (!Directory.Exists(directory))
            {
                removed.Add(launch);
            }
        }

        return removed;
    }
}
