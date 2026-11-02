using System.Globalization;
using Aw.Sindri.Core.Locks;

namespace Aw.Sindri.Infrastructure.Locks;

/// <summary>
/// Removes the 'heavy-session/&lt;pid&gt;.lock' folders a SIGKILLed scope left behind: the heavy wrapper reclaims a dead slot only when its key is tried again, and nobody
/// reuses those keys. A slot goes only when its owner record names a dead process and the key (the folder name) is a dead pid too; a slot with no readable record goes
/// only once it is older than <see cref="NoRecordGrace"/> (the wrapper's mkdir comes before its owner write). The 'heavy-job.lock*' slots are never touched.
/// Removal is the wrapper's own release: the owner file, then the folder, which is never removed while anything else is in it.
/// </summary>
/// <param name="clock">The clock the age of a record-less slot is judged on.</param>
internal sealed class HeavyLockSweep(TimeProvider clock) : IHeavyLockSweep
{
    /// <summary>How old a slot with no readable owner record must be before it is removed.</summary>
    internal static readonly TimeSpan NoRecordGrace = TimeSpan.FromMinutes(1);

    private const string SessionFolder = "heavy-session";

    public int Sweep(string locksDir)
    {
        ArgumentNullException.ThrowIfNull(locksDir);
        var folder = Path.Combine(locksDir, SessionFolder);
        if (!Directory.Exists(folder))
        {
            return 0;
        }

        var removed = 0;
        foreach (var slot in Directory.EnumerateDirectories(folder, "*.lock"))
        {
            removed += TrySweep(slot) ? 1 : 0;
        }

        return removed;
    }

    private bool TrySweep(string slot)
    {
        try
        {
            return Stale(slot) && Release(slot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;   // a holder released it, or took it, meanwhile
        }
    }

    private bool Stale(string slot)
    {
        var keyAlive = int.TryParse(Path.GetFileNameWithoutExtension(slot), NumberStyles.None, CultureInfo.InvariantCulture, out var key) && HolderRecords.Alive(key);
        if (keyAlive)
        {
            return false;
        }

        return HolderRecords.RecordOf(slot) is { } record
            ? HolderRecords.Dead(record)
            : clock.GetUtcNow() - new DateTimeOffset(Directory.GetLastWriteTimeUtc(slot), TimeSpan.Zero) > NoRecordGrace;
    }

    private static bool Release(string slot)
    {
        File.Delete(Path.Combine(slot, DirectoryHeavySlots.OwnerFile));
        Directory.Delete(slot, recursive: false);
        return true;
    }
}
