using Aw.Context.Core;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Ranking;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Features.QueryContext;

internal sealed partial class QueryPipeline
{
    // The answer's notes: what the search left out under a pin, and for an empty answer where the results read were (concept pins spec §3.2), never a fall back to the unscoped search.
    private async ValueTask<IReadOnlyList<string>?> AnswerNotes(GraphPart graph, (int Fetched, List<DegradedSource> Degraded, IReadOnlyList<string> Searched) answer, CancellationToken ct)
    {
        await NoteQueuedDecisions(graph, answer.Degraded, ct).ConfigureAwait(false);
        List<string> notes = [.. answer.Searched];
        if (graph.Pins.Scope is { } scoped && answer.Fetched == 0 && graph.Candidates.Count == 0)
        {
            notes.Add(graph.Pins.Skipped.EmptyNote(scoped.Pin, matchesNothing: !scoped.HoldsAnyFile()));
        }

        return notes.Count == 0 ? null : notes;
    }

    private async Task<Searched> Search(IReadOnlyList<IContextSource> sources, IReadOnlyList<string> terms, (DateTimeOffset Deadline, DateTimeOffset? AsOf) when,
        (PinScope? Pin, PinTally? Skipped) pinned, CancellationToken ct)
    {
        var deadline = when.Deadline;
        var answers = new List<SourceAnswer>();
        var degraded = new List<DegradedSource>();
        var left = new List<string>();
        var searchable = sources.Where(s => s.Describe().Id != _linearLive).ToList();   // Linear answers come from the owned index only (#1531)
        if (pinned.Pin is not null)
        {
            // A source that can't filter by pin is left out and said so, never searched unscoped.
            var unscoped = sources.Where(s => !s.Describe().HonorsPin).ToList();
            left.AddRange(unscoped.Select(s => $"{s.Describe().Id.Value}: left out under concept= (it can't be scoped to a pin)"));
            searchable = [.. searchable.Where(s => s.Describe().HonorsPin)];
        }

        var calls = searchable.Select(async source =>
        {
            var query = new SourceQuery(terms, when.AsOf, Limit: SourceQuery.DefaultLimit, deadline - clock.GetUtcNow()) { Pin = pinned.Pin?.Counting(pinned.Skipped) };
            var hits = await SourceCalls.Within(source, (query.Deadline, clock), t => source.Search(query, t), ct).ConfigureAwait(false);
            var notes = source is ISourceNotes noted ? noted.TakeNotes() : [];
            return (source.Describe().Id, hits, notes);
        });
        foreach (var (id, hits, notes) in await Task.WhenAll(calls).ConfigureAwait(false))
        {
            degraded.AddRange(notes.Select(note => new DegradedSource(id, ContextErrors.SourceUnavailable.Code, note, clock.GetUtcNow())));
            if (hits.IsSuccess)
            {
                answers.Add(new SourceAnswer(id, hits.Value));
            }
            else
            {
                degraded.Add(SourceCalls.Degraded(id, hits.Error, clock.GetUtcNow()));
            }
        }

        return new Searched(answers, degraded) { Notes = left };
    }
}
