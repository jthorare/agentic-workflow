using System.Text;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The per-launch script a tmux session runs (#1765): the session's environment and argv live in a 0600 file in a 0700
/// directory instead of on the 'new-session' command line, which tmux caps near 16 KB. The script deletes its own
/// directory as its first step, so the env (which can carry a secret) is gone before the command runs.
/// </summary>
internal static class TmuxLaunchScript
{
    private const string FileName = "launch.sh";

    /// <summary>Writes the script into a fresh private directory under <paramref name="root"/>.</summary>
    /// <param name="root">The directory to make the per-launch directory in.</param>
    /// <param name="session">The session to run.</param>
    /// <returns>The path of the script's directory; <see cref="PathOf"/> gives the script.</returns>
    internal static string Write(string root, HostSession session)
    {
        _ = Directory.CreateDirectory(root);
        var dir = Path.Combine(root, "snd-" + Guid.NewGuid().ToString("N")[..12]);
        _ = Directory.CreateDirectory(dir);
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using var file = new FileStream(PathOf(dir), options);
            using var writer = new StreamWriter(file, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { NewLine = "\n" };
            writer.Write(Text(dir, session));
        }
        catch
        {
            DeleteQuietly(dir);
            throw;
        }

        return dir;
    }

    /// <summary>The script inside <paramref name="dir"/>.</summary>
    /// <param name="dir">A directory <see cref="Write"/> returned.</param>
    /// <returns>The script's path.</returns>
    internal static string PathOf(string dir) => Path.Combine(dir, FileName);

    /// <summary>The script text: one braced block, so sh has parsed all of it before the first command (the delete) removes the file.</summary>
    /// <param name="dir">The directory the script deletes.</param>
    /// <param name="session">The session to run.</param>
    /// <returns>The script.</returns>
    internal static string Text(string dir, HostSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var script = new StringBuilder("{\n");
        _ = script.Append("rm -rf -- ").Append(Quote(dir)).Append('\n');
        foreach (var (key, value) in session.Env)
        {
            _ = script.Append("export ").Append(key).Append('=').Append(Quote(value)).Append('\n');
        }

        _ = script.Append("exec ").AppendJoin(' ', session.Argv.Select(Quote)).Append("\n}\n");
        return script.ToString();
    }

    /// <summary>Removes <paramref name="dir"/>; an IO error is ignored (best effort).</summary>
    /// <param name="dir">The directory.</param>
    internal static void DeleteQuietly(string dir)
    {
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
            // best effort
        }
        catch (UnauthorizedAccessException)
        {
            // best effort
        }
    }

    private static string Quote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
