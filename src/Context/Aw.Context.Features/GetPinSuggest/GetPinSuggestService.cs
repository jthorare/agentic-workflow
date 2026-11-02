using Aw.Context.Core;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetPinSuggest;

/// <summary>
/// The pin-suggest read (concept pins spec §3.6): one concept's terms with the records each would newly link, the features a search finds, or the concept each line of a pasted list matches.
/// Reads only. A term's count is made over the records the caller may see (the audiences the query pipeline gives the caller's repos; a session also its map's linked records), live, unblocked and in the
/// intent layer, whose text holds the term by the alias rule and that the concept doesn't link yet by any rule. A record of another repo than the one answered for is left out; one with no repo is judged by its audience.
/// </summary>
internal sealed class GetPinSuggestService(ConceptReader reader, IIntentStore intents, IRecordStore records) : IService<GetPinSuggestRequest, PinSuggestView>
{
    private const int PageSize = 100;

    public async ValueTask<Result<PinSuggestView>> Handle(GetPinSuggestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = request.Query;
        var read = await reader.Read(request.Scope, query.Read, query.Repo, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var (basis, graph, text) = (read.Value.Basis, read.Value.Graph, read.Value.Text);
        if (query.Concept is { } concept)
        {
            return await Terms(request, read.Value, concept, cancellationToken).ConfigureAwait(false);
        }

        var features = query.Search is { } search ? PinSuggestBuilder.Search(graph, text, search, query.Limit) : null;
        var matches = query.Lines is { } lines ? PinSuggestBuilder.Match(graph, text, lines) : null;
        return new PinSuggestView(1, basis.Repo, basis.MapVersion, Terms: null, features, matches);
    }

    private async ValueTask<Result<PinSuggestView>> Terms(GetPinSuggestRequest request, ConceptRead read, NodeKey concept, CancellationToken cancellationToken)
    {
        var words = PinSuggestBuilder.TermsOf(read.Graph, concept);
        if (words.IsError)
        {
            return words.Error;
        }

        var counts = await NewRecords(request, read, concept, words.Value, cancellationToken).ConfigureAwait(false);
        return counts.IsError
            ? counts.Error
            : new PinSuggestView(1, read.Basis.Repo, read.Basis.MapVersion, PinSuggestBuilder.TermRows(read.Graph, concept, read.Text, words.Value, counts.Value), PinSuggestBuilder.MembersOf(read.Graph, concept, read.Text), Matches: null);
    }

    // Per term, the visible live records in the intent layer that hold it by the alias rule and that the concept doesn't link yet. Each record is read once.
    private async ValueTask<Result<Dictionary<string, int>>> NewRecords(GetPinSuggestRequest request, ConceptRead read, NodeKey concept, IReadOnlyList<string> words, CancellationToken cancellationToken)
    {
        var counts = words.ToDictionary(static w => w, static _ => 0, StringComparer.Ordinal);
        var settings = SettingsReader.Read(request.Scope.Settings);
        var layer = await intents.Layer(cancellationToken).ConfigureAwait(false);
        if (settings.IsError || layer.IsError)
        {
            return settings.IsError ? settings.Error : layer.Error;
        }

        var audience = await Audience(settings.Value, request.Scope, request.Query.Repo, read.Graph, cancellationToken).ConfigureAwait(false);
        if (audience.IsError)
        {
            return audience.Error;
        }

        var unlinked = layer.Value.Records.Select(static r => r.Key.Value).ToHashSet(StringComparer.Ordinal);
        unlinked.ExceptWith(layer.Value.Links.Where(l => l.Target.Value == concept.Value).Select(static l => l.Record.Value));
        var walked = await Walk(new Candidates(unlinked, audience.Value, read.Basis.Repo), words, counts, cancellationToken).ConfigureAwait(false);
        return walked.IsError ? walked.Error : counts;
    }

    // Every stored record, a page at a time.
    private async ValueTask<Result<Unit>> Walk(Candidates candidates, IReadOnlyList<string> words, Dictionary<string, int> counts, CancellationToken cancellationToken)
    {
        RecordKey? after = null;
        while (true)
        {
            var page = await records.List(new RecordFilter(), after, PageSize, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            var seen = await Tally(page.Value.Where(candidates.Counts), words, counts, cancellationToken).ConfigureAwait(false);
            if (seen.IsError || page.Value.Count < PageSize)
            {
                return seen;
            }

            after = page.Value[^1].Key;
        }
    }

    // Adds one to the count of every term each record's text holds.
    private async ValueTask<Result<Unit>> Tally(IEnumerable<StoredRecord> visible, IReadOnlyList<string> words, Dictionary<string, int> counts, CancellationToken cancellationToken)
    {
        foreach (var stored in visible)
        {
            var chunks = await records.Chunks(stored.Key, cancellationToken).ConfigureAwait(false);
            if (chunks.IsError)
            {
                return chunks.Error;
            }

            var text = string.Join('\n', chunks.Value.OrderBy(static c => c.Seq).Select(static c => c.Text));   // scrubbed at ingest
            foreach (var word in RulesLinker.Matching(text, words))
            {
                counts[word]++;
            }
        }

        return Unit.Value;
    }

    // The mask the query pipeline builds (QueryPipeline.WithRecordStore): the audiences of the repos in scope (the repo a human named, when they named one), a human's Slack channels, and a session's map records.
    private static async ValueTask<Result<AudienceScope>> Audience(ContextSettings settings, ContextScope scope, string? repo, GraphSnapshot graph, CancellationToken cancellationToken)
    {
        var resolved = ScopeRules.Resolve(settings, scope.Repos, scope.Caller);
        if (resolved.IsError && resolved.Error.Code != ContextErrors.UnknownAnchor.Code)
        {
            return resolved.Error;
        }

        // UnknownAnchor is "no repo in scope": count over no repo's audience rather than fail the read
        var inScope = resolved.IsError ? [] : resolved.Value;
        var covered = inScope.Where(r => repo is null || string.Equals(r.Name.Value, repo, StringComparison.Ordinal)).ToList();
        var allowed = await RecordMask.Allowed(settings, covered, scope.Caller.Kind, cancellationToken).ConfigureAwait(false);
        if (allowed.IsError)
        {
            return allowed.Error;
        }

        return new AudienceScope(allowed.Value) { Records = scope.Caller.Kind == ContextCallerKind.Session ? RecordMask.Linked(graph) : new HashSet<RecordKey>() };
    }

    // The records a count is made over: live, unblocked, in the layer but not linked to the concept yet, and visible. The audience is allowed (or, for a session, the record is in its scoped map);
    // a record that names another repo than the one answered for is out, one that names none is judged by its audience.
    private sealed record Candidates(HashSet<string> Unlinked, AudienceScope Audience, string? AnsweredFor)
    {
        public bool Counts(StoredRecord record) =>
            record.RemovedAt is null && !record.Blocked && Unlinked.Contains(record.Key.Value) && Audience.Allows(record.Audience, record.Key)
            && (AnsweredFor is null || record.Repo is not { } repo || string.Equals(repo.Value, AnsweredFor, StringComparison.Ordinal));
    }
}
