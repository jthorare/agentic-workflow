using System.Globalization;

namespace Aw.Context.Core.Map;

/// <summary>Doctor 'context:store': warns while the write-ahead log is over 1 GiB and larger than the database itself.</summary>
public static class StoreHealth
{
    /// <summary>The row's name.</summary>
    public const string Name = "context:store";
    private const long WarnBytes = 1024L * 1024 * 1024;

    /// <summary>Judges the store's file sizes.</summary>
    /// <param name="sizes">The sizes, null when there is no store file yet.</param>
    /// <returns>The row.</returns>
    public static MapHealth Check(StoreFileSizes? sizes)
    {
        if (sizes is null)
        {
            return new MapHealth(Name, MapHealthState.Ok, "no store yet", Fix: null);
        }

        if (sizes.WalBytes <= WarnBytes || sizes.WalBytes <= sizes.DatabaseBytes)
        {
            return new MapHealth(Name, MapHealthState.Ok, $"context.db {Size(sizes.DatabaseBytes)}, its log {Size(sizes.WalBytes)}", Fix: null);
        }

        var ratio = (double)sizes.WalBytes / Math.Max(sizes.DatabaseBytes, 1);
        // The restart fix works because the store's connections are unpooled: when the last one closes SQLite checkpoints and deletes the -wal file; a PASSIVE autocheckpoint never shrinks it.
        return new MapHealth(Name, MapHealthState.Warn,
            string.Create(CultureInfo.InvariantCulture, $"context.db's log is {Size(sizes.WalBytes)}, {ratio:0.0}x the {Size(sizes.DatabaseBytes)} database: a long-running reader or no quiet moment keeps it from resetting"),
            "restart Sindri (sindri stop, then sindri) to drop old readers; the log resets at the next checkpoint");
    }

    private static string Size(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value:0.0} {units[unit]}");
    }
}
