using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// The heavy-job lock directories, the same on every OS. A lock slot is a directory (the lock path, then .2, .3 ...) whose
/// owner file starts with the holder's pid; a slot with no readable owner is listed with no pid. The holders are checked with one batch
/// of IProcessLiveness; one the host can't be asked about is listed alive.
/// </summary>
internal static class HeavyLockDirectory
{
    internal static async ValueTask<Result<IReadOnlyList<HeavyLock>>> List(string heavyLock, IProcessLiveness liveness, CancellationToken cancellationToken)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(heavyLock));
        if (parent is null || !Directory.Exists(parent))
        {
            return Results.Success<IReadOnlyList<HeavyLock>>([]);
        }

        var name = Path.GetFileName(heavyLock);
        var slots = new List<(string Slot, string Holder, ProcessClaim? Claim)>();
        foreach (var slot in Directory.EnumerateDirectories(parent).Where(d => IsSlot(Path.GetFileName(d), name)).Order(StringComparer.Ordinal))
        {
            slots.Add(await Holder(slot, cancellationToken).ConfigureAwait(false));
        }

        // one batch for every holder; a holder the host could not be asked about is listed alive (unknown is not stale)
        var claims = slots.Select(s => s.Claim).OfType<ProcessClaim>().ToList();
        var verdicts = await liveness.Running(claims, cancellationToken).ConfigureAwait(false);
        var taken = new List<HeavyLock>();
        var next = 0;
        foreach (var (slot, holder, claim) in slots)
        {
            taken.Add(claim is null ? new HeavyLock(slot, Pid: null, Alive: false, holder) : new HeavyLock(slot, claim.Pid, verdicts[next++].IsLiveOrUnknown(), holder));
        }

        return taken;
    }

    private static bool IsSlot(string directory, string name) =>
        directory == name
        || (directory.StartsWith(name + ".", StringComparison.Ordinal) && int.TryParse(directory[(name.Length + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out _));

    // The owner file's time is when the lock was taken: a process that started after it is not the holder (the pid was reused).
    private static async ValueTask<(string Slot, string Holder, ProcessClaim? Claim)> Holder(string slot, CancellationToken cancellationToken)
    {
        try
        {
            var owner = Path.Combine(slot, "owner");
            var text = (await File.ReadAllTextAsync(owner, cancellationToken).ConfigureAwait(false)).Trim();
            var first = text.Split(' ', 2)[0];
            return int.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
                ? (slot, text, new ProcessClaim(pid, new DateTimeOffset(File.GetLastWriteTimeUtc(owner), TimeSpan.Zero)))
                : (slot, text, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (slot, string.Empty, null);
        }
    }
}
