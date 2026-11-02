using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// ISessionCap under an exclusive lock on one file in Sindri's real-home 'locks' directory. FileShare.None is a flock on Unix, so the kernel
/// drops it when the holder dies, and two opens in one process exclude each other too.
/// </summary>
/// <param name="lockFile">The lock file, for example '&lt;real home&gt;/locks/sessions.lock'; its directory is created.</param>
/// <param name="scopes">The live scopes.</param>
internal sealed class FileSessionCap(string lockFile, ISliceScopes scopes) : ISessionCap
{
    private static readonly TimeSpan _poll = TimeSpan.FromMilliseconds(20);

    /// <inheritdoc/>
    public async Task<Result<T>> Start<T>(int cap, Func<CancellationToken, Task<Result<T>>> start, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        var held = await Acquire(cancellationToken).ConfigureAwait(false);
        await using var _ = held.ConfigureAwait(false);
        var live = await scopes.Live(cancellationToken).ConfigureAwait(false);
        if (live.Count >= cap)
        {
            return SandboxRefusal.For(
                SindriErrors.LaunchSessionWait, SandboxReasons.Sessions,
                string.Create(CultureInfo.InvariantCulture, $"{live.Count} of {cap} sessions are running (fix: wait for one to end, or raise sandbox.limits.sessions)"),
                details: new Dictionary<string, string>(StringComparer.Ordinal) { ["running"] = live.Count.ToString(CultureInfo.InvariantCulture), ["cap"] = cap.ToString(CultureInfo.InvariantCulture) });
        }

        return await start(cancellationToken).ConfigureAwait(false);
    }

    private async Task<FileStream> Acquire(CancellationToken cancellationToken)
    {
        // why: a full path always has a directory part.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(lockFile))!);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.None);
            }
            catch (IOException)
            {
                // Held by another launch: wait and try again.
                await Task.Delay(_poll, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
