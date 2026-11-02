using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Locks;
using Aw.Evidence.Infrastructure.Native;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Locks;

/// <summary>
/// Machine locks as directories, interoperable with config/lib/locks.sh and the heavy wrapper: a slot is held while its
/// directory exists, taken with an atomic mkdir and polled every second. The QA-stack lock has one slot. The heavy-job
/// lock has AW_HEAVY_SLOTS slots (default 2): slot 1 is its path, slot k is '&lt;path&gt;.k', and each poll tries them in
/// order, as the heavy wrapper does, so a holder that knows only the path (a plain locks.sh caller) occupies slot 1 and
/// the total never exceeds the slot count. The holder writes its pid to &lt;slot&gt;/pid (the file locks.sh reads) and
/// '&lt;pid&gt; &lt;utc&gt; ui-evidence' to &lt;slot&gt;/owner.
/// With AW_HEAVY_LOCK_HELD=1 a parent already holds a heavy-job slot, so taking it is a no-op. A waiter that finds a slot
/// whose recorded pid (pid, else the first word of owner) names a dead process frees it (<see cref="StaleLock"/>), as
/// locks.sh does; a slot with no holder file yet is left alone.
/// </summary>
internal sealed class DirectoryLocks(LockSettings settings, TimeProvider clock, TextWriter notices) : IMachineLocks
{
    /// <summary>The file a holder writes inside the lock directory.</summary>
    internal const string OwnerFile = "owner";

    /// <summary>The file holding the holder's pid alone, the one config/lib/locks.sh reads.</summary>
    internal const string PidFile = "pid";

    private static readonly TimeSpan _poll = TimeSpan.FromSeconds(1);

    private int _slotNoticeWritten;

    /// <inheritdoc/>
    public async ValueTask<Result<ILockLease>> AcquireAsync(LockKind kind, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (kind == LockKind.QaStack)
        {
            return await WaitAsync(kind, settings.QaStack, [settings.QaStack], timeout, cancellationToken).ConfigureAwait(false);
        }

        if (settings.HeavyJobHeld)
        {
            return DirectoryLease.Inherited(kind);
        }

        await NoticeIgnoredSlotsAsync(cancellationToken).ConfigureAwait(false);
        return await WaitAsync(kind, settings.HeavyJob, HeavySlots(), timeout, cancellationToken).ConfigureAwait(false);
    }

    private string[] HeavySlots() =>
        [.. Enumerable.Range(1, settings.HeavyJobSlots).Select(k => k == 1 ? settings.HeavyJob : string.Create(CultureInfo.InvariantCulture, $"{settings.HeavyJob}.{k}"))];

    private async ValueTask NoticeIgnoredSlotsAsync(CancellationToken cancellationToken)
    {
        if (settings.IgnoredHeavyJobSlots is { } ignored && Interlocked.Exchange(ref _slotNoticeWritten, 1) == 0)
        {
            var notice = string.Create(CultureInfo.InvariantCulture, $"ui-evidence: AW_HEAVY_SLOTS='{ignored}' isn't a whole number of at least 1; using {settings.HeavyJobSlots} slots");
            await notices.WriteLineAsync(notice.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<Result<ILockLease>> WaitAsync(LockKind kind, string name, string[] slots, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = clock.GetUtcNow() + timeout;
        var announced = false;
        while (true)
        {
            var taken = TryTakeAny(slots);
            if (taken.IsError)
            {
                return taken.Error;
            }

            if (taken.Value is { } slot)
            {
                return Hold(kind, slot);
            }

            var left = deadline - clock.GetUtcNow();
            if (left <= TimeSpan.Zero)
            {
                var what = slots.Length == 1 ? name : string.Create(CultureInfo.InvariantCulture, $"{name} ({slots.Length} slots)");
                return EvidenceErrors.LockBusy.With(string.Create(CultureInfo.InvariantCulture, $"{what} stayed held for {timeout.TotalSeconds:0.#} s"));
            }

            if (!announced)
            {
                await notices.WriteLineAsync($"ui-evidence: waiting on lock: {name}".AsMemory(), cancellationToken).ConfigureAwait(false);
                announced = true;
            }

            await Task.Delay(left < _poll ? left : _poll, clock, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Result<string?> TryTakeAny(string[] slots)
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
                return slot;
            }
        }

        return (string?)null;
    }

    private static Result<bool> TryTake(string directory)
    {
        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(directory) ?? directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return EvidenceErrors.FileUnavailable.With($"can't create the lock directory's parent for {directory}: {ex.Message}");
        }

        var errno = Libc.MakeDirectory(directory);
        if (errno == Libc.AlreadyExists && StaleLock.TryReclaim(directory))
        {
            errno = Libc.MakeDirectory(directory);
        }

        return errno switch
        {
            0 => true,
            Libc.AlreadyExists => false,
            _ => EvidenceErrors.FileUnavailable.With(string.Create(CultureInfo.InvariantCulture, $"mkdir {directory} failed (errno {errno})")),
        };
    }

    private DirectoryLease Hold(LockKind kind, string directory)
    {
        var owner = string.Create(CultureInfo.InvariantCulture, $"{Environment.ProcessId} {clock.GetUtcNow().UtcDateTime:yyyy-MM-ddTHH:mm:ssZ} ui-evidence\n");
        try
        {
            File.WriteAllText(Path.Combine(directory, PidFile), string.Create(CultureInfo.InvariantCulture, $"{Environment.ProcessId}\n"));
            File.WriteAllText(Path.Combine(directory, OwnerFile), owner);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The pid and owner lines say who holds the lock; the lock itself is the directory, already held.
        }

        return new DirectoryLease(kind, directory);
    }
}
