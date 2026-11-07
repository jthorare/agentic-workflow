namespace Aw.Context.Infrastructure.Linear;

/// <summary>
/// The write key: 'context.sources.linear.writeToken' resolved through the environment Composition passes (Linear hub spec 2026-11-04 D3). Only <see cref="LinearWriteClient"/> holds one (an architecture test checks it), so no read path can reach
/// the secret. It goes only into an Authorization header, never a log line or a message.
/// </summary>
/// <param name="pointer">The settings' write token pointer.</param>
/// <param name="environment">Resolves a pointer to its secret.</param>
internal sealed class LinearWriteKey(string pointer, Func<string, string?> environment)
{
    private readonly LinearKeyCache _cache = new(() => environment(pointer));

    /// <summary>True when a resolution has finished.</summary>
    public bool Settled => _cache.Settled;

    /// <summary>The secret, or null when the pointer didn't resolve.</summary>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The secret or null.</returns>
    public Task<string?> Get(CancellationToken cancellationToken) => _cache.Get(cancellationToken);

    /// <summary>Drops the kept secret, so the next call asks the keychain again (Linear refused it; it may have been rotated).</summary>
    public void Forget() => _cache.Forget();
}
