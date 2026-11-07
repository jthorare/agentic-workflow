namespace Aw.Context.Infrastructure.Linear;

/// <summary>
/// Resolves one secret pointer once, off the calling thread: the caching the read key and the write key share. A resolution that gives nothing or throws is not kept (it counts as not resolved, never as an exception), so a keychain that was
/// locked a moment ago is asked again on the next call.
/// </summary>
/// <param name="resolve">Resolves the pointer to the secret, or null.</param>
internal sealed class LinearKeyCache(Func<string?> resolve)
{
    private readonly Lock _lock = new();
    private Task<string?>? _key;

    /// <summary>True when a resolution has finished (whatever it gave); false when none started or one is still waiting, as on a locked keychain.</summary>
    public bool Settled
    {
        get
        {
            lock (_lock)
            {
                return _key is { IsCompleted: true };
            }
        }
    }

    /// <summary>The secret, or null when the pointer didn't resolve.</summary>
    /// <param name="cancellationToken">Stops the wait (the resolution itself runs on).</param>
    /// <returns>The secret or null.</returns>
    public async Task<string?> Get(CancellationToken cancellationToken)
    {
        Task<string?> resolving;
        lock (_lock)
        {
            resolving = _key ??= Task.Run(resolve, CancellationToken.None);
        }

        try
        {
            var key = await resolving.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(key))
            {
                Forget(resolving);
            }

            return key;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Forget(resolving);
            return null;
        }
    }

    /// <summary>Drops the kept secret, so the next call resolves again (a key that Linear refused may have been rotated).</summary>
    public void Forget()
    {
        lock (_lock)
        {
            _key = null;
        }
    }

    private void Forget(Task<string?> resolving)
    {
        lock (_lock)
        {
            if (ReferenceEquals(_key, resolving))
            {
                _key = null;
            }
        }
    }
}
