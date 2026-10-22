using System.Globalization;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>Reads lsof's field output (-F) for the macOS collector: a working directory, the open descriptors and the unix sockets that have a path.</summary>
internal static class MacLsof
{
    private const string SocketPrefix = "socket:[";

    /// <summary>The working directory in "lsof -a -p PID -d cwd -F n".</summary>
    /// <param name="output">lsof's output.</param>
    /// <returns>The absolute path, or null.</returns>
    internal static string? Cwd(string output) =>
        Records(output).Select(static r => r.Name).FirstOrDefault(static n => n is { Length: > 0 } && n[0] == '/');

    /// <summary>The targets of the descriptors in "lsof -a -p PID -d 0-65535 -F ftdn": a file's path, and 'socket:[key]' for a unix socket (see <see cref="SocketKey"/>), the form a Linux descriptor link has.</summary>
    /// <param name="output">lsof's output.</param>
    /// <param name="limit">The most targets to keep.</param>
    /// <returns>The targets in descriptor order.</returns>
    internal static IReadOnlyList<string> OpenFiles(string output, int limit) =>
        [.. Records(output).Select(Target).OfType<string>().Take(limit)];

    /// <summary>Key to path for every unix socket in "lsof -a -U -u UID -F pftdn" that is bound to a path.</summary>
    /// <param name="output">lsof's output.</param>
    /// <returns>The map.</returns>
    internal static IReadOnlyDictionary<long, string> Sockets(string output)
    {
        var found = new Dictionary<long, string>();
        foreach (var record in Records(output))
        {
            if (IsUnix(record) && SocketKey(record.Device) is { } key && record.Name is { Length: > 0 } name && name[0] == '/')
            {
                found[key] = name;
            }
        }

        return found;
    }

    /// <summary>The number a socket's lsof device column ('0x826cb2cf7ecb93da', its kernel address) stands for.</summary>
    /// <param name="device">The column's value.</param>
    /// <returns>The key, or null when it isn't a hexadecimal address.</returns>
    internal static long? SocketKey(string? device) =>
        device is { Length: > 2 } && device.StartsWith("0x", StringComparison.Ordinal) && ulong.TryParse(device.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var address)
            ? unchecked((long)address)
            : null;

    /// <summary>The 'socket:[key]' form of a key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The link text.</returns>
    internal static string SocketLink(long key) => string.Create(CultureInfo.InvariantCulture, $"{SocketPrefix}{key}]");

    private static string? Target(Record record) => IsUnix(record) && SocketKey(record.Device) is { } key ? SocketLink(key)
        : record.Name is { Length: > 0 } name && name[0] == '/' ? name
        : null;

    private static bool IsUnix(Record record) => string.Equals(record.Type, "unix", StringComparison.Ordinal);

    /// <summary>One descriptor's fields: lsof starts it with its f line and gives the t, d and n lines after.</summary>
    private sealed record Record(string? Type, string? Device, string? Name);

    // why: -F output is one field per line, the first character naming the field; a p line starts a process and an f line a descriptor, whose t, d and n lines follow
    private static List<Record> Records(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var records = new List<Record>();
        string? type = null;
        string? device = null;
        string? name = null;
        var open = false;
        foreach (var line in output.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (line[0])
            {
                case 'p' or 'f':
                    Flush();
                    (type, device, name, open) = (null, null, null, line[0] == 'f');
                    break;
                case 't':
                    type = line[1..];
                    break;
                case 'd':
                    device = line[1..];
                    break;
                case 'n':
                    name = line[1..];
                    break;
                default:
                    break;
            }
        }

        Flush();
        return records;

        void Flush()
        {
            if (open)
            {
                records.Add(new Record(type, device, name));
            }
        }
    }
}
