using Aw.Kernel;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>Keeps the pasted keys: into the OS keychain, with a pointer (never the key) in the live profile.</summary>
internal interface ILinearConnectionStore
{
    /// <summary>Whether <paramref name="key"/> is the stored read key.</summary>
    /// <param name="key">The pasted key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when it is the same key; false when it differs or no read key is stored.</returns>
    public ValueTask<bool> IsReadKey(string key, CancellationToken cancellationToken);

    /// <summary>Stores the read key and writes its pointer and the picked teams into the live profile.</summary>
    /// <param name="key">The pasted key.</param>
    /// <param name="teams">The team keys to read.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success; SND-SECRET-004 when no store took it; SND-INDEX-011 when the profile has no `context.sources.linear` entry.</returns>
    public ValueTask<Result<Unit>> SaveRead(string key, IReadOnlyList<string> teams, CancellationToken cancellationToken);

    /// <summary>Stores the write key in the OS keychain and writes its pointer into the live profile.</summary>
    /// <param name="key">The pasted key.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success; SND-LINEAR-014 when no keychain took it (nothing is saved, and no file holds the key).</returns>
    public ValueTask<Result<Unit>> SaveWrite(string key, CancellationToken cancellationToken);

    /// <summary>Removes the write key's pointer from the live profile (the stored keychain entry stays: Sindri has no way to delete one yet).</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when a write key was set; false when there was none.</returns>
    public ValueTask<Result<bool>> RemoveWrite(CancellationToken cancellationToken);
}
