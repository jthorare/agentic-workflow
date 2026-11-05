using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Locks;

namespace Aw.Sindri.Infrastructure.Locks;

/// <summary>
/// The heavy-job lock as directories, interoperable with config/lib/locks.sh, the heavy wrapper and Evidence's directory locks: a
/// slot is held while its directory exists, taken with an atomic mkdir and polled every second. Slot 1 is the lock's path and slot k
/// is '&lt;path&gt;.k', tried in order, so a holder that knows only the path occupies slot 1 and the total never exceeds the slot count.
/// The holder writes '&lt;pid&gt; &lt;utc&gt; &lt;holder&gt;' to &lt;slot&gt;/owner. With AW_HEAVY_LOCK_HELD=1 a parent already holds a slot, so taking
/// one is a no-op. Like the heavy wrapper, a slot whose recorded owner process is gone is reclaimed; a slot with a pid file (config/lib/locks.sh's) is judged the same way; a slot with no entries at all is freed once over <see cref="EmptyGrace"/> old, and one with other files is held (see <see cref="NoRecordGrace"/>).
/// </summary>
/// <param name="settings">Where the lock is and how many slots it has.</param>
/// <param name="clock">The clock the wait runs on.</param>
internal sealed class DirectoryHeavySlots(HeavyLockSettings settings, TimeProvider clock) : IHeavySlots
{
    /// <summary>The file a holder writes inside the lock directory.</summary>
    internal const string OwnerFile = "owner";

    /// <summary>The file config/lib/locks.sh's holder writes: its pid on one line.</summary>
    internal const string PidFile = "pid";

    /// <summary>How old a slot with no entries at all must be before it is freed: younger is a holder between its mkdir and its record write.</summary>
    internal static readonly TimeSpan EmptyGrace = TimeSpan.FromMinutes(1);

    /// <summary>How old a slot with no record at all must be before it is taken (the TS sindri's last resort).</summary>
    internal static readonly TimeSpan NoRecordGrace = TimeSpan.FromHours(6);

    private static readonly TimeSpan _poll = TimeSpan.FromSeconds(1);

    /// <summary>Called with a slot's path after it was judged stale and before it is taken over (tests use it to play the other waiter).</summary>
    internal Action<string> BeforeTakeover { get; init; } = static _ => { };

    /// <summary>Called with a slot's path right after our mkdir made it (tests use it to make the owner write fail).</summary>
    internal Action<string> AfterMkdir { get; init; } = static _ => { };

    /// <summary>Writes the owner file (tests use it to fail a write after the file was created, as a full disk does).</summary>
    internal Action<string, string> WriteFile { get; init; } = static (path, text) => File.WriteAllText(path, text);

    /// <summary>Reads the claimed record back (tests use it to fail the read after the record was moved aside).</summary>
    internal Func<string, string> ReadFile { get; init; } = File.ReadAllText;

    public async ValueTask<Result<HeavySlotTaken>> Take(string holder, TimeSpan timeout, IProgress<string>? waiting, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(holder);
        if (settings.Held)
        {
            return new HeavySlotTaken(HeavySlotLease.Inherited, string.Empty);
        }

        var slots = Slots();
        var deadline = clock.GetUtcNow() + timeout;
        var announced = false;
        while (true)
        {
            var taken = TryTakeAny(slots, holder);
            if (taken.IsError || taken.Value is not null)
            {
                return taken.IsError ? taken.Error : new HeavySlotTaken(taken.Value, string.Empty);
            }

            var left = deadline - clock.GetUtcNow();
            if (left <= TimeSpan.Zero)
            {
                return new HeavySlotTaken(Lease: null, Holders(slots));
            }

            if (!announced && waiting is not null)
            {
                waiting.Report($"waiting for heavy-job lock ({Holders(slots)})");
                announced = true;
            }

            await Task.Delay(left < _poll ? left : _poll, clock, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Whether an owner line names this process.</summary>
    /// <param name="ownerLine">The owner file's text.</param>
    /// <returns>True when its first word is our pid.</returns>
    internal static bool IsOurs(string ownerLine) => ownerLine.Split(' ', 2)[0] == Environment.ProcessId.ToString(CultureInfo.InvariantCulture);

    private string[] Slots() =>
        [.. Enumerable.Range(1, settings.Slots).Select(k => k == 1 ? settings.Lock : string.Create(CultureInfo.InvariantCulture, $"{settings.Lock}.{k}"))];

    private Result<HeavySlotLease?> TryTakeAny(string[] slots, string holder)
    {
        foreach (var slot in slots)
        {
            var taken = TryTake(slot);
            if (taken.IsError)
            {
                return taken.Error;
            }

            if (taken.Value)
            {
                if (WriteOwner(slot, holder) is { } refusal)
                {
                    return SindriErrors.ServiceFailed.With(refusal);
                }

                return new HeavySlotLease(slot);
            }
        }

        return (HeavySlotLease?)null;
    }

    private Result<bool> TryTake(string directory)
    {
        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(directory) ?? directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ServiceFailed.With($"can't create the lock directory's parent for {directory}: {ex.Message}");
        }

        var errno = Libc.MakeDirectory(directory);
        if (errno == Libc.AlreadyExists && Reclaim(directory))
        {
            errno = Libc.MakeDirectory(directory);
        }

        if (errno == 0)
        {
            AfterMkdir(directory);
        }

        return errno switch
        {
            0 => true,
            Libc.AlreadyExists => false,
            _ => SindriErrors.ServiceFailed.With(string.Create(CultureInfo.InvariantCulture, $"mkdir {directory} failed (errno {errno})")),
        };
    }

    /// <summary>
    /// Removes a stale slot, once, by compare and delete. The record that proves it is our own '&lt;slot&gt;/owner' or the TS sindri's
    /// '&lt;slot&gt;.holder.json' (beside the slot, and only trusted when it is not older than the slot): a pid that is gone on this host, or alive but
    /// started after the record was written (a reused pid), as the heavy wrapper judges the first. A slot held over <see cref="NoRecordGrace"/> is stale
    /// whoever holds it (the TS sindri's last resort, for a crashed shell holder nobody will release); one with other files and no record is otherwise held, and so is one whose owner file is empty (a holder mid-write). The record is first renamed to a
    /// name of its own, which only one reclaimer can win; the winner reads it again and removes the slot only when it is byte for byte the record
    /// judged stale, else puts it back. The heavy wrapper renames the record aside the same way, but the two sides share no mutex, so its reclaim can
    /// still land between our last delete and the folder's removal; the folder is never removed while anything is in it. A reclaimer killed between its
    /// rename and its delete leaves a slot holding only 'owner.reclaim.N' or 'pid.reclaim.N' files: when every such N (the reclaimer's pid, in our names
    /// too) is gone on this host, or a reused pid, the slot is freed; a live N is a reclaimer mid-step, and a name with no pid waits for the age grace, which also
    /// beats a live N. A claim that fails after its rename puts the record back. False when the
    /// slot is held, was taken meanwhile, or can't be removed.
    /// </summary>
    private bool Reclaim(string directory)
    {
        try
        {
            var record = HolderRecords.RecordOf(directory);
            var aged = Aged(directory);
            if (record is null && IsEmpty(directory))
            {
                return Age(directory) > EmptyGrace && EmptyStill(directory);
            }

            if (record is null ? !(aged || ReclaimLeftovers.AllDead(directory)) : !(aged || HolderRecords.Dead(record)))
            {
                return false;
            }

            BeforeTakeover(directory);
            if (record is null)
            {
                return RemoveRecordless(directory);
            }

            return Claim(record) && TryRemove(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // Re-judged after BeforeTakeover, since a new holder's slot is fresh: aged, or holding only leftovers of dead reclaimers, and still no record or owner.
    private bool RemoveRecordless(string directory)
    {
        var aged = Aged(directory);
        return (aged || ReclaimLeftovers.AllDead(directory)) && HolderRecords.RecordOf(directory) is null && !File.Exists(Path.Combine(directory, OwnerFile))
            && ReclaimLeftovers.Clear(directory, aged) && TryRemove(directory);
    }

    private TimeSpan Age(string directory) => clock.GetUtcNow() - new DateTimeOffset(Directory.GetLastWriteTimeUtc(directory), TimeSpan.Zero);

    private bool Aged(string directory) => Age(directory) > NoRecordGrace;

    private static bool IsEmpty(string directory) => !Directory.EnumerateFileSystemEntries(directory).Any();

    // rmdir is atomic: it fails if a holder wrote into the folder after the check.
    private bool EmptyStill(string directory)
    {
        BeforeTakeover(directory);
        return TryRemove(directory);
    }

    // Takes the judged record out of the way, and only that record: if what we moved isn't the text judged stale, it goes back.
    private bool Claim(HolderRecord record)
    {
        var claimed = ReclaimLeftovers.NameFor(record.Path);
        File.Move(record.Path, claimed);   // atomic: of two reclaimers of one record, the second finds nothing to move
        try
        {
            if (string.Equals(ReadFile(claimed), record.Text, StringComparison.Ordinal))
            {
                File.Delete(claimed);
                return true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // fall through: never leave a name of ours behind
        }

        ReclaimLeftovers.PutBack(claimed, record.Path);
        return false;
    }

    private static bool TryRemove(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // Null when the record is written. A failed write may have created the file, so the file goes first, then the folder; the slot is called released only when it really is.
    private string? WriteOwner(string directory, string holder)
    {
        var line = string.Create(CultureInfo.InvariantCulture, $"{Environment.ProcessId} {clock.GetUtcNow().UtcDateTime:yyyy-MM-ddTHH:mm:ssZ} {holder}\n");
        var owner = Path.Combine(directory, OwnerFile);
        try
        {
            WriteFile(owner, line);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var removed = TryDelete(owner) && TryRemove(directory);
            return removed
                ? $"can't write the owner record in {directory} ({ex.Message}), so the slot was released"
                : $"can't write the owner record in {directory} ({ex.Message}) and couldn't remove the slot: it stays held until that folder is removed by hand";
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>'slot 1/2 held by 4242 sindri context-map-build, slot 2/2 held by 77 heavy …': what the waiter and the refusal say.</summary>
    private static string Holders(string[] slots) => string.Join(", ", slots.Select((slot, i) =>
    {
        var owner = Path.Combine(slot, OwnerFile);
        var by = File.Exists(owner) ? SafeRead(owner) : "an unknown holder";
        return string.Create(CultureInfo.InvariantCulture, $"slot {i + 1}/{slots.Length} held by {by}");
    }));

    private static string SafeRead(string owner)
    {
        try
        {
            var parts = File.ReadAllText(owner).Trim().Split(' ', 3);
            return parts.Length == 3 ? $"{parts[0]} ({parts[2]}, since {parts[1]})" : string.Join(' ', parts);
        }
        catch (IOException)
        {
            return "an unknown holder";
        }
    }
}
