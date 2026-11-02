using System.Diagnostics;
using System.Globalization;

namespace Aw.Sindri.Infrastructure.Locks;

/// <summary>Reads who holds a slot (our owner file or the TS sindri's holder.json) and judges whether that holder is dead.</summary>
internal static class HolderRecords
{
    private static readonly System.Text.Json.JsonSerializerOptions _json = Kernel.Json.AwJson.CreateOptions(HolderJsonContext.Default);

    // A record's time is written to the second, after the process started: this much slack keeps a holder that took the slot at once from reading as a reused pid.
    private static readonly TimeSpan _startTolerance = TimeSpan.FromSeconds(5);

    internal static HolderRecord? RecordOf(string directory)
    {
        var owner = Path.Combine(directory, DirectoryHeavySlots.OwnerFile);
        if (File.Exists(owner))
        {
            var text = File.ReadAllText(owner);
            var words = text.Split(' ', 3);
            return int.TryParse(words[0], NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
                ? new HolderRecord(owner, text, pid, Host: null, words.Length > 1 && DateTimeOffset.TryParse(words[1], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedAt) ? parsedAt : null)
                : null;
        }

        var beside = directory + ".holder.json";
        if (!File.Exists(beside) || File.GetLastWriteTimeUtc(beside) < Directory.GetLastWriteTimeUtc(directory))
        {
            return null;   // no record, or a leftover from a holder that took the slot without one
        }

        var json = File.ReadAllText(beside);
        var parsed = Kernel.Json.AwJson.Deserialize<TsHolderRecord>(System.Text.Encoding.UTF8.GetBytes(json), _json);
        return parsed.IsError   // a bad document is no record
            ? null
            : new HolderRecord(beside, json, parsed.Value.Pid, parsed.Value.Host, DateTimeOffset.TryParse(parsed.Value.StartedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var written) ? written : null);
    }

    // Dead on this host: the pid is gone, or a live process of that pid started after the record was written (the pid was reused). The host is compared up to its
    // first '.' on both sides, since .NET's MachineName cuts the domain that os.hostname() keeps.
    internal static bool Dead(HolderRecord record) =>
        (record.Host is null || string.Equals(ShortHost(record.Host), ShortHost(Environment.MachineName), StringComparison.OrdinalIgnoreCase))
        && (!Alive(record.Pid) || StartedAfter(record.Pid, record.At));

    private static string ShortHost(string host) => host.Split('.', 2)[0];

    private static bool StartedAfter(int pid, DateTimeOffset? recorded)
    {
        if (recorded is null)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            return process.StartTime.ToUniversalTime() > recorded.Value.UtcDateTime + _startTolerance;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    internal static bool Alive(int pid)
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
        catch (InvalidOperationException)
        {
            return false;
        }
    }

}
