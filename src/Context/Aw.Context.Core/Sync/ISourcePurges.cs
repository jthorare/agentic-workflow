using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Sync;

/// <summary>Whether a purge of a source's records is running. A tick holds the same lock a purge does, so the lock alone cannot tell them apart; a write-through lands during a tick and skips during a purge.</summary>
public interface ISourcePurges
{
    /// <summary>Whether a purge (a store change that holds the source's lock) is running for the source right now; a tick running is not a purge.</summary>
    /// <param name="source">The source.</param>
    /// <returns>True while a purge holds the source.</returns>
    public bool IsPurging(SourceId source);
}
