using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Core.Records;

/// <summary>The store of who the owner is in each source, one row per source. Every method answers an error, never throws, for a store that can't be read ('SND-CONTEXT-004').</summary>
public interface ISourceIdentityStore
{
    /// <summary>Reads a source's identity.</summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The identity, or null when none was ever written.</returns>
    public ValueTask<Result<SourceIdentity?>> Read(SourceId source, CancellationToken cancellationToken);

    /// <summary>Writes a source's identity, replacing the one it had.</summary>
    /// <param name="identity">The identity.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success once stored.</returns>
    public ValueTask<Result<Unit>> Write(SourceIdentity identity, CancellationToken cancellationToken);
}
