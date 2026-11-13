using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Ranking;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Features.QueryContext;

internal sealed partial class QueryPipeline
{
    private async Task<IReadOnlyList<FetchedRecord>> Fetch(List<FusedItem> items, (IReadOnlyList<IContextSource> Sources, IIngestFilter Ingest) call,
        DateTimeOffset deadline, List<DegradedSource> degraded, CancellationToken ct)
    {
        var (sources, ingest) = call;
        var gone = new Dictionary<SourceId, int>();
        var read = await ReadBatches(items, sources, deadline, degraded, ct).ConfigureAwait(false);
        var fetched = new List<FetchedRecord>();
        for (var next = 0; next < items.Count; next++)
        {
            if (read[next] is not { } record)
            {
                continue;   // its source's batch didn't run or failed: already noted
            }

            var item = items[next];
            var hit = item.Hits[0];
            if (record.IsError)
            {
                if (record.Error.Kind == ErrorKind.NotFound)
                {
                    gone[hit.Ref.Source] = gone.GetValueOrDefault(hit.Ref.Source) + 1;
                }
                else if (degraded.TrueForAll(d => d.Source != hit.Ref.Source))
                {
                    degraded.Add(SourceCalls.Degraded(hit.Ref.Source, record.Error, clock.GetUtcNow()));
                }

                continue;   // a record gone since the search, or a source that stopped answering: no claim without a citation
            }

            var source = sources.First(s => s.Describe().Id == hit.Ref.Source);
            var text = record.Value.Blocked   // the adapter's own whole-document clean withheld it: cite it as scrub-blocked
                ? new ScrubbedField(string.Empty, Redacted: false, Blocked: true)
                : ingest.Clean(record.Value.Text, singleLine: false);   // cleaned whole and kept whole
            var aliases = item.Aliases.Select(a => ingest.Clean(a, singleLine: true)).ToList();   // record text too: cleaned like the title
            fetched.Add(new FetchedRecord(item, source, ingest.Clean(record.Value.Title, singleLine: true), text, aliases));
        }

        NoteGone(gone, fetched, degraded);
        return fetched;
    }

    // One FetchMany call per source (#3085), sources in the order of their best-ranked item; the answers land at each item's rank.
    private async Task<Result<SourceRecord>?[]> ReadBatches(List<FusedItem> items, IReadOnlyList<IContextSource> sources, DateTimeOffset deadline, List<DegradedSource> degraded, CancellationToken ct)
    {
        var read = new Result<SourceRecord>?[items.Count];
        foreach (var group in Enumerable.Range(0, items.Count).GroupBy(i => items[i].Hits[0].Ref.Source))
        {
            if (deadline - clock.GetUtcNow() <= TimeSpan.Zero)
            {
                NoteUnread(group.Select(i => items[i]), degraded);   // no claim without a citation: what wasn't fetched in time isn't shown, but the answer says so
                continue;
            }

            var id = group.Key;
            var source = sources.First(s => s.Describe().Id == id);
            var indexes = group.ToList();
            var refs = indexes.ConvertAll(i => items[i].Hits[0].Ref);
            var batch = await SourceCalls.Within(source, (deadline - clock.GetUtcNow(), clock), t => source.FetchMany(refs, t), ct).ConfigureAwait(false);
            if (batch.IsError && ContextErrors.IsMissedDeadline(batch.Error))
            {
                NoteUnread(indexes.Select(i => items[i]), degraded);   // the deadline cut the whole batch off: its hits are unread, the source isn't broken
                continue;
            }

            if (batch.IsError || batch.Value.Count != refs.Count)
            {
                if (degraded.TrueForAll(d => d.Source != id))
                {
                    degraded.Add(SourceCalls.Degraded(id, batch.IsError ? batch.Error : ContextErrors.SourceUnavailable.With($"{id.Value} answered {batch.Value.Count} of {refs.Count} records"), clock.GetUtcNow()));
                }

                continue;
            }

            Place(items, indexes, batch.Value, read, degraded);
        }

        return read;
    }

    // The batch's answers go to their ranks; a read the deadline cut off is noted as unread, not as a failure of the source.
    private void Place(List<FusedItem> items, List<int> indexes, IReadOnlyList<Result<SourceRecord>> answers, Result<SourceRecord>?[] read, List<DegradedSource> degraded)
    {
        var unread = new List<FusedItem>();
        for (var n = 0; n < indexes.Count; n++)
        {
            if (answers[n].IsError && ContextErrors.IsMissedDeadline(answers[n].Error))
            {
                unread.Add(items[indexes[n]]);
            }
            else
            {
                read[indexes[n]] = answers[n];
            }
        }

        NoteUnread(unread, degraded);
    }

    // A source whose every matching record was gone when read says so: an empty answer is never silent.
    private void NoteGone(Dictionary<SourceId, int> gone, List<FetchedRecord> fetched, List<DegradedSource> degraded)
    {
        foreach (var (id, count) in gone.Where(g => degraded.TrueForAll(d => d.Source != g.Key) && !fetched.Exists(f => f.Source.Describe().Id == g.Key)))
        {
            degraded.Add(new DegradedSource(id, ContextErrors.SourceUnavailable.Code,
                $"{id.Value} found {count.ToString(CultureInfo.InvariantCulture)} matching record(s), but each was gone (not found) when read, so none is shown: check the source's index against its documents (sindri context sources status)", clock.GetUtcNow()));
        }
    }

    /// <summary>Tells, per source, that records its search found were not read because the query's deadline ran out first: a missing record is never read as no match.</summary>
    private void NoteUnread(IEnumerable<FusedItem> unread, List<DegradedSource> degraded)
    {
        foreach (var group in unread.GroupBy(i => i.Hits[0].Ref.Source))
        {
            if (degraded.TrueForAll(d => d.Source != group.Key))
            {
                degraded.Add(new DegradedSource(group.Key, ContextErrors.SourceUnavailable.Code,
                    $"{group.Key.Value} missed the query's deadline: {group.Count().ToString(CultureInfo.InvariantCulture)} matching record(s) were found but not read in time, so they are not shown (raise 'context.deadline')", clock.GetUtcNow()));
            }
        }
    }
}
