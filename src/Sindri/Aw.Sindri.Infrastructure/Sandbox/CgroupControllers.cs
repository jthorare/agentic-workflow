using System.Globalization;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The cgroup controllers a systemd user manager has been delegated (sandbox spec §3.3 needs memory, cpu and pids).</summary>
internal static class CgroupControllers
{
    private static readonly string[] _needed = ["memory", "cpu", "pids"];

    /// <summary>Reads the real uid from the text of /proc/self/status ('Uid: real effective saved fs').</summary>
    /// <param name="statusText">The file's text, or null.</param>
    /// <returns>The uid, or null.</returns>
    internal static int? UidFrom(string? statusText)
    {
        var line = statusText?.Split('\n').FirstOrDefault(l => l.StartsWith("Uid:", StringComparison.Ordinal));
        var field = line?.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault();
        return int.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out var uid) ? uid : null;
    }

    /// <summary>Gets where the user manager's delegated controllers are listed.</summary>
    /// <param name="uid">The real uid.</param>
    /// <returns>The path.</returns>
    internal static string FileFor(int uid)
    {
        var id = uid.ToString(CultureInfo.InvariantCulture);
        return $"/sys/fs/cgroup/user.slice/user-{id}.slice/user@{id}.service/cgroup.controllers";
    }

    /// <summary>Finds the first needed controller the list lacks.</summary>
    /// <param name="listText">The controllers file's text, or null when it cannot be read (then nothing is claimed).</param>
    /// <returns>The missing controller, or null.</returns>
    internal static string? FirstMissing(string? listText)
    {
        if (listText is null)
        {
            return null;
        }

        var have = listText.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return _needed.FirstOrDefault(c => !have.Contains(c, StringComparer.Ordinal));
    }
}
