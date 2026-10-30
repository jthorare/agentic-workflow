using Aw.Context.Core;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Ranking;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Features.QueryContext;

internal sealed partial class QueryPipeline
{
    private static readonly SourceId _pinNote = SourceId.From("concept");

    private async ValueTask AnswerNotes(GraphPart graph, (int Fetched, List<DegradedSource> Degraded) answer, CancellationToken ct)
    {
        NoteEmptyPin(graph, answer.Fetched, answer.Degraded);
        await NoteQueuedDecisions(graph, answer.Degraded, ct).ConfigureAwait(false);
    }

    // Under concept= an answer with nothing in it says where the results read were (concept pins spec §3.2), never falling back to the unscoped search.
    private void NoteEmptyPin(GraphPart graph, int fetched, List<DegradedSource> degraded)
    {
        if (graph.Pins.Scope is { } scoped && fetched == 0 && graph.Candidates.Count == 0)
        {
            degraded.Add(new DegradedSource(_pinNote, ContextErrors.SourceUnavailable.Code, graph.Pins.Skipped.EmptyNote(scoped.Pin, matchesNothing: !scoped.HoldsAnyFile()), clock.GetUtcNow()));
        }
    }

    private async Task<Searched> Search(IReadOnlyList<IContextSource> sources, IReadOnlyList<string> terms, (DateTimeOffset Deadline, DateTimeOffset? AsOf) when,
        (PinScope? Pin, PinTally? Skipped) pinned, CancellationToken ct)
    {
        var deadline = when.Deadline;
        var answers = new List<SourceAnswer>();
        var degraded = new List<DegradedSource>();
        var searchable = sources.Where(s => s.Describe().Id != _linearLive).ToList();   // Linear answers come from the owned index only (#1531)
        if (pinned.Pin is not null)
        {
            // A source that can't filter by pin is left out and said so, never searched unscoped.
            var unscoped = sources.Where(s => !s.Describe().HonorsPin).ToList();
            degraded.AddRange(unscoped.Select(s => new DegradedSource(s.Describe().Id, ContextErrors.SourceUnavailable.Code, "left out under concept= (it can't be scoped to a pin)", clock.GetUtcNow())));
            searchable = [.. searchable.Where(s => s.Describe().HonorsPin)];
        }

        var calls = searchable.Select(async source =>
        {
            var query = new SourceQuery(terms, when.AsOf, Limit: SourceQuery.DefaultLimit, deadline - clock.GetUtcNow()) { Pin = pinned.Pin, Skipped = pinned.Skipped };
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

        return new Searched(answers, degraded);
    }
}
