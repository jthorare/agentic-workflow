using Aw.Context.Core;
using Aw.Context.Core.Ranking;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Features.QueryContext;

internal sealed partial class QueryPipeline
{
    /// <summary>The item shape's own record comes first in the record store's answer, then what the search found (not again if it found it too).</summary>
    private static async Task<Searched> WithAnchorRecord(Searched searched, (GraphPart Graph, IReadOnlyList<IContextSource> Sources) from, CancellationToken ct)
    {
        if (from.Graph.AnchorRecord is not { } key || from.Sources.OfType<RecordStoreSource>().FirstOrDefault() is not { } store)
        {
            return searched;
        }

        var anchor = await store.Anchor(key, ct).ConfigureAwait(false);
        if (anchor.IsError || anchor.Value.Count == 0)
        {
            return searched;   // gone or not the caller's: the search answers as it did
        }

        var found = searched.Answers.FirstOrDefault(a => a.Source == RecordStoreSource.Id)?.Hits ?? [];
        var hits = anchor.Value.Concat(found.Where(h => h.Ref.Canonical != key)).Select((h, i) => h with { Rank = i + 1 }).ToList();
        return searched with { Answers = [.. searched.Answers.Where(a => a.Source != RecordStoreSource.Id), new SourceAnswer(RecordStoreSource.Id, hits)] };
    }

    /// <summary>
    /// A record linked to the answer's nodes whose decisions are still queued (labeling off or over its cap) is told as degraded 042 for 'decisions',
    /// so a missing decision is not read as no decision (verification plan J2.3). Nothing is asked of the model.
    /// </summary>
    private async Task NoteQueuedDecisions(GraphPart graph, List<DegradedSource> degraded, CancellationToken ct)
    {
        if (graph.LinkedRecords.Count == 0)
        {
            return;
        }

        var waiting = new HashSet<string>(StringComparer.Ordinal);
        var feeds = new HashSet<SourceId>();
        foreach (var record in graph.LinkedRecords)
        {
            var stored = await lexical.Store.Get(RecordKey.From(record), ct).ConfigureAwait(false);
            if (stored is { IsError: false, Value: { } found })
            {
                _ = feeds.Add(found.Source);   // the queue is kept under the feed that fetched the record
            }
        }

        foreach (var feed in feeds)
        {
            var queued = await intents.Queued(feed, ct).ConfigureAwait(false);
            waiting.UnionWith(queued.IsError ? [] : queued.Value.Select(k => k.Value));
        }

        if (graph.LinkedRecords.Any(waiting.Contains))
        {
            degraded.Add(new DegradedSource(SourceId.From("decisions"), ContextErrors.LabelingUnavailable.Code,
                "decisions of a linked record are not extracted yet: labeling is off or over its token cap (fix: turn on 'context.labeling.enabled' or raise 'maxTokensPerSync', then run sindri context sources sync, or wait for the next context-sync slot)", clock.GetUtcNow()));
        }
    }
}
