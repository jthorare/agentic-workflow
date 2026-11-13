using Aw.Kernel;

namespace Aw.Context.Core.Sources;

/// <summary>
/// A read-only source adapter (spec §5.1). Every call returns a Result: an Unavailable error lets the query go on
/// without the source (degraded), NotFound means the record is gone, anything else is fatal for that call. There is
/// no rate limiting (D6); each call gets the query's remaining deadline. Adapters return raw text: sanitizing,
/// scrubbing and trust tagging happen once, in the engine. ContextSourceContract&lt;TSource&gt; is its contract suite.
/// </summary>
public interface IContextSource
{
    /// <summary>What this source is.</summary>
    /// <returns>Its descriptor.</returns>
    public SourceDescriptor Describe();

    /// <summary>Searches the source.</summary>
    /// <param name="query">The terms, cut-off, limit and deadline.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Hits in rank order (rank 1 first), at most the limit.</returns>
    public ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken);

    /// <summary>Fetches one record at the version its reference names.</summary>
    /// <param name="reference">The record.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The record, or NotFound when that version is gone.</returns>
    public ValueTask<Result<SourceRecord>> Fetch(SourceRef reference, CancellationToken cancellationToken);

    /// <summary>
    /// Fetches many records in one call (#3085): a source that can read them together (repo docs: one git batch read per repo)
    /// overrides this; the default reads them one at a time through <see cref="Fetch"/>, so every source keeps working. When the token fires mid-way the default keeps what it read and marks each remaining record <see cref="ContextErrors.MissedDeadline"/>.
    /// </summary>
    /// <param name="references">The records, in the order to read them.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One result per reference, in order (a record gone is that entry's NotFound), or an error when the whole call failed.</returns>
    public async ValueTask<Result<IReadOnlyList<Result<SourceRecord>>>> FetchMany(IReadOnlyList<SourceRef> references, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(references);
        var records = new List<Result<SourceRecord>>(references.Count);
        foreach (var reference in references)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;   // the deadline fired: what was read stays, the rest is marked below
            }

            try
            {
                records.Add(await Fetch(reference, cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        while (records.Count < references.Count)
        {
            records.Add(ContextErrors.MissedDeadline(Describe().Id.Value));   // one per record not read, so the pipeline counts them as unread
        }

        return records;
    }

    /// <summary>The current version of each reference, in one batched call; a record that is gone has none.</summary>
    /// <param name="refs">The references to check.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One entry per reference, in order: its current version, or null when the record is gone.</returns>
    public ValueTask<Result<IReadOnlyList<SourceVersion?>>> Versions(IReadOnlyList<SourceRef> refs, CancellationToken cancellationToken);

    /// <summary>Checks the source can answer.</summary>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>Its health; never throws.</returns>
    public ValueTask<SourceHealth> Probe(CancellationToken cancellationToken);
}
