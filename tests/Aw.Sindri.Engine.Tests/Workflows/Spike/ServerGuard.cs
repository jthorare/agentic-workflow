using System.Diagnostics;
using System.Globalization;
using System.Text;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>
/// Ties a pinned Temporal server to the test host that started it. A host killed with SIGKILL never runs Dispose, and the
/// Temporal SDK (and TemporalDevServer, which detaches on purpose for 'sindri serve') leave the server running; 63 of them
/// took the box down on 2026-10-14. <see cref="Wrap(string, int)"/> returns a small launcher script that starts a watchdog
/// and then execs the real server, so the server keeps the script's pid and looks like itself in 'ps'. The watchdog polls
/// the owner and the server: once the owner is gone it sends SIGTERM, then SIGKILL 2 s later; once the server is gone
/// first it just exits. Production code is untouched: only the path the tests hand to the SDK or the launcher changes.
/// </summary>
internal static class ServerGuard
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<string>> _launchers = new(StringComparer.Ordinal);


    private const string DirectoryPrefix = "aw-sindri-test-guard-";

    private const string Watchdog =
        "alive() { kill -0 \"$1\" 2>/dev/null || return 1; case $(ps -o stat= -p \"$1\" 2>/dev/null) in Z*) return 1;; esac; }; "
        + "owner=$1; target=$2; "
        + "while alive \"$owner\" && alive \"$target\"; do sleep 1; done; "
        + "alive \"$owner\" && exit 0; "
        + "alive \"$target\" || exit 0; kill -TERM \"$target\" 2>/dev/null; sleep 2; "
        + "alive \"$target\" && kill -KILL \"$target\" 2>/dev/null; exit 0";

    /// <summary>The server executable to hand to the SDK or the dev-server launcher.</summary>
    /// <param name="executable">The pinned server binary.</param>
    /// <param name="ownerPid">The process the server must not outlive.</param>
    /// <returns>The launcher script's path (the binary itself on Windows, where these tests don't run servers this way).</returns>
    public static string Wrap(string executable, int ownerPid)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ownerPid);
        if (OperatingSystem.IsWindows())
        {
            return executable;
        }

        // why: one writer per (owner, executable): another test class may be exec'ing the script this very moment
        var key = string.Create(CultureInfo.InvariantCulture, $"{ownerPid}\n{executable}");
        var launcher = _launchers.GetOrAdd(key, static (_, state) => new Lazy<string>(() => Write(state.Executable, state.OwnerPid)), (Executable: executable, OwnerPid: ownerPid));
        try
        {
            return launcher.Value;
        }
        catch
        {
            _ = _launchers.TryRemove(new KeyValuePair<string, Lazy<string>>(key, launcher));   // why: a failed write isn't cached for the whole host
            throw;
        }
    }

    private static string Write(string executable, int ownerPid)
    {
        if (OperatingSystem.IsWindows())
        {
            return executable;
        }

        var directory = Path.Combine(Path.GetTempPath(), DirectoryPrefix + Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        if (!Directory.Exists(directory))
        {
            // why: first launcher of this host; folders of hosts that died by SIGKILL are swept now, this host's own goes at process exit
            Sweep(Path.GetTempPath());
            _ = Directory.CreateDirectory(directory);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => RemoveOwn(directory);
        }

        var script = new StringBuilder();
        _ = script.Append(CultureInfo.InvariantCulture, $"/bin/sh -c '{Watchdog.Replace("'", "'\\''", StringComparison.Ordinal)}' aw-test-guard {ownerPid} $$ </dev/null >/dev/null 2>&1 &\n");
        _ = script.Append(CultureInfo.InvariantCulture, $"exec '{executable.Replace("'", "'\\''", StringComparison.Ordinal)}' \"$@\"\n");
        var name = string.Create(CultureInfo.InvariantCulture, $"{ownerPid}-{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(executable)))[..12]}.sh");
        var path = Path.Combine(directory, name);

        // why: another host may share the name only if pids repeat; write complete and executable under a private name, then rename atomically
        var staging = Path.Combine(directory, name + "." + Path.GetRandomFileName());
        try
        {
            _ = ExecutableScript.Write(staging, script.ToString());   // warm-exec: the guard exits before the watchdog and before Temporal, and no write descriptor is left to rename
            File.Move(staging, path, overwrite: true);
        }
        catch
        {
            File.Delete(staging);
            throw;
        }

        return path;
    }

    /// <summary>The server executable for this test host.</summary>
    /// <param name="executable">The pinned server binary.</param>
    /// <returns>The path to start.</returns>
    public static string Wrap(string executable) => Wrap(executable, Environment.ProcessId);

    /// <summary>Deletes this host's launcher folder; the servers already exec'd their script, so nothing reads it after exit.</summary>
    private static void RemoveOwn(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // why: best effort at exit; the next host sweeps it once this pid is gone
        }
        catch (UnauthorizedAccessException)
        {
            // why: same
        }
    }

    /// <summary>Deletes the launcher folders of hosts that are gone.</summary>
    private static void Sweep(string tempRoot)
    {
        foreach (var folder in Directory.EnumerateDirectories(tempRoot, DirectoryPrefix + "*"))
        {
            var owner = Path.GetFileName(folder)[DirectoryPrefix.Length..];
            if (int.TryParse(owner, NumberStyles.None, CultureInfo.InvariantCulture, out var pid) && !Exists(pid))
            {
                try
                {
                    Directory.Delete(folder, recursive: true);
                }
                catch (IOException)
                {
                    // why: another host sweeping at the same moment
                }
            }
        }
    }

    private static bool Exists(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
