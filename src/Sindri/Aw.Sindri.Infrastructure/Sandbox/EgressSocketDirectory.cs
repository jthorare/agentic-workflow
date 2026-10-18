using System.Globalization;
using System.Text;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The short per-launch socket directory (sandbox spec §3.2, R2-20).</summary>
internal static class EgressSocketDirectory
{
    /// <summary>The longest socket path accepted (macOS 104 bytes, Linux 108, minus the NUL).</summary>
    public const int MaxPathBytes = 103;

    /// <summary>The proxy socket's file name.</summary>
    public const string ProxySocket = "proxy.sock";

    /// <summary>The Sindri socket's file name.</summary>
    public const string SindriSocket = "sindri.sock";

    /// <summary>Makes the path of a new directory: $XDG_RUNTIME_DIR/sindri/(8 hex), else /tmp/sindri-(uid)/(8 hex).</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="uid">The user id.</param>
    /// <param name="eightHex">Eight hex digits naming the launch.</param>
    /// <returns>The directory path (not created).</returns>
    public static string PathFor(Func<string, string?> environment, uint uid, string eightHex)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return environment("XDG_RUNTIME_DIR") is { Length: > 0 } runtime && Path.IsPathRooted(runtime)
            ? Path.Combine(runtime, "sindri", eightHex)
            : Path.Combine("/tmp", "sindri-" + uid.ToString(CultureInfo.InvariantCulture), eightHex);
    }

    /// <summary>Whether both sockets under <paramref name="directory"/> fit the limit.</summary>
    /// <param name="directory">The directory.</param>
    /// <returns>True when both fit.</returns>
    public static bool SocketPathsFit(string directory) =>
        Encoding.UTF8.GetByteCount(Path.Combine(directory, ProxySocket)) <= MaxPathBytes
        && Encoding.UTF8.GetByteCount(Path.Combine(directory, SindriSocket)) <= MaxPathBytes;

    /// <summary>Creates the directory, 0700 (parents too).</summary>
    /// <param name="directory">The directory.</param>
    public static void Create(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        if (OperatingSystem.IsWindows())
        {
            _ = Directory.CreateDirectory(directory);
            return;
        }

        const UnixFileMode ownerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        var parent = Path.GetDirectoryName(directory);
        if (parent is { Length: > 0 } && !Directory.Exists(parent))
        {
            _ = Directory.CreateDirectory(parent, ownerOnly);
        }

        _ = Directory.CreateDirectory(directory, ownerOnly);
    }
}
