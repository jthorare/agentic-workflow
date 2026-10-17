using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Health;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// The heavy-job lock directories, the same on every OS. A lock slot is a directory (the lock path, then .2, .3 ...) whose
/// owner file starts with the holder's pid; a slot with no readable owner is listed with no pid.
/// </summary>
internal static class HeavyLockDirectory
{
    internal static async ValueTask<Result<IReadOnlyList<HeavyLock>>> List(string heavyLock, CancellationToken cancellationToken)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(heavyLock));
        if (parent is null || !Directory.Exists(parent))
        {
            return Results.Success<IReadOnlyList<HeavyLock>>([]);
        }

        var name = Path.GetFileName(heavyLock);
        var taken = new List<HeavyLock>();
        foreach (var slot in Directory.EnumerateDirectories(parent).Where(d => IsSlot(Path.GetFileName(d), name)).Order(StringComparer.Ordinal))
        {
            taken.Add(await Holder(slot, cancellationToken).ConfigureAwait(false));
        }

        return taken;
    }

    private static bool IsSlot(string directory, string name) =>
        directory == name
        || (directory.StartsWith(name + ".", StringComparison.Ordinal) && int.TryParse(directory[(name.Length + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out _));

    private static async ValueTask<HeavyLock> Holder(string slot, CancellationToken cancellationToken)
    {
        try
        {
            var text = (await File.ReadAllTextAsync(Path.Combine(slot, "owner"), cancellationToken).ConfigureAwait(false)).Trim();
            var first = text.Split(' ', 2)[0];
            return int.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
                ? new HeavyLock(slot, pid, ProcessLiveness.IsAlive(pid), text)
                : new HeavyLock(slot, Pid: null, Alive: false, text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new HeavyLock(slot, Pid: null, Alive: false, Holder: string.Empty);
        }
    }
}
