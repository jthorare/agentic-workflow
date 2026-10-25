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
        var fetched = new List<FetchedRecord>();
        var gone = new Dictionary<SourceId, int>();
        for (var next = 0; next < items.Count; next++)
        {
            var item = items[next];
            if (deadline - clock.GetUtcNow() <= TimeSpan.Zero)
            {
                NoteUnread(items.Skip(next), degraded);   // no claim without a citation: what wasn't fetched in time isn't shown, but the answer says so
                break;
            }

            var hit = item.Hits[0];
            var source = sources.First(s => s.Describe().Id == hit.Ref.Source);
            var record = await SourceCalls.Within(source, (deadline - clock.GetUtcNow(), clock), t => source.Fetch(hit.Ref, t), ct).ConfigureAwait(false);
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

            var text = record.Value.Blocked   // the adapter's own whole-document clean withheld it: cite it as scrub-blocked
                ? new ScrubbedField(string.Empty, Redacted: false, Blocked: true)
                : ingest.Clean(record.Value.Text, singleLine: false);   // cleaned whole and kept whole
            var aliases = item.Aliases.Select(a => ingest.Clean(a, singleLine: true)).ToList();   // record text too: cleaned like the title
            fetched.Add(new FetchedRecord(item, source, ingest.Clean(record.Value.Title, singleLine: true), text, aliases));
        }

        NoteGone(gone, fetched, degraded);
        return fetched;
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
