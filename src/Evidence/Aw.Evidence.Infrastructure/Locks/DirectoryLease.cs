using Aw.Evidence.Core.Locks;

namespace Aw.Evidence.Infrastructure.Locks;

/// <summary>
/// A held lock directory. Release deletes the pid and owner files this process wrote, then the directory; twice is harmless. A
/// directory whose owner line names another process is left alone: someone removed ours by hand and another holder
/// took the slot.
/// </summary>
internal sealed class DirectoryLease(LockKind kind, string? directory) : ILockLease
{
    private int _released;

    /// <summary>A lease that holds nothing on disk: the lock is already held by a parent process.</summary>
    /// <param name="kind">The lock.</param>
    /// <returns>The lease.</returns>
    internal static DirectoryLease Inherited(LockKind kind) => new(kind, directory: null);

    /// <inheritdoc/>
    public IReadOnlyList<LockKind> Held { get; } = [kind];

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (directory is null || Interlocked.Exchange(ref _released, 1) == 1)
        {
            return;
        }

        try
        {
            var owner = Path.Combine(directory, DirectoryLocks.OwnerFile);
            if (File.Exists(owner) && !IsOurs(await File.ReadAllTextAsync(owner).ConfigureAwait(false)))
            {
                return;
            }

            File.Delete(owner);
            File.Delete(Path.Combine(directory, DirectoryLocks.PidFile));
            Directory.Delete(directory, recursive: false);
        }
        catch (IOException)
        {
            // Something else is inside the directory; leave it, as the shell's release does (rmdir || true).
        }
        catch (UnauthorizedAccessException)
        {
            // Not ours to remove; same as above.
        }
    }

    private static bool IsOurs(string ownerLine) =>
        ownerLine.Split(' ', 2)[0] == Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
