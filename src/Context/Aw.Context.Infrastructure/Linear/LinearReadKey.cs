using Aw.Context.Core.Linear;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>The read key: 'context.sources.linear.token' resolved through the environment Composition passes (Sindri resolves pointers, spec §4), once per client. It never touches the write key's pointer.</summary>
/// <param name="pointer">The settings' token pointer, or null when the profile names none.</param>
/// <param name="environment">Resolves a pointer to its secret.</param>
/// <param name="credential">The host's own order (the connected Linear connection first, then the pointer), or null for the pointer alone.</param>
internal sealed class LinearReadKey(string? pointer, Func<string, string?> environment, ILinearReadCredential? credential = null)
{
    private readonly LinearKeyCache _cache = new(() => credential is not null ? credential.TokenFor(pointer) : pointer is null ? null : environment(pointer));

    /// <summary>True when a resolution has finished.</summary>
    public bool Settled => _cache.Settled;

    /// <summary>The secret, or null when the pointer didn't resolve.</summary>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The secret or null.</returns>
    public Task<string?> Get(CancellationToken cancellationToken) => _cache.Get(cancellationToken);
}
