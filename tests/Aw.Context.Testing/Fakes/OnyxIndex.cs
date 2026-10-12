using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Context.Testing.Fixtures;

namespace Aw.Context.Testing.Fakes;

/// <summary>
/// The recorded Onyx documents ('onyx/documents.json': each a SearchDoc and its chunk texts) and a deterministic search
/// over them: a document scores by the share of query terms it contains (plus a tiny weight per occurrence), ties broken
/// by newest 'updated_at', then document id. Not Onyx's ranking (Sindri ranks, D3); only stable and explainable.
/// </summary>
internal static class OnyxIndex
{
    private static readonly HashSet<string> _stopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "about", "does", "for", "how", "in", "is", "it", "of", "on", "or", "the", "to", "was", "what", "which",
    };

    private static readonly Lazy<List<OnyxDocument>> _documents = new(Load);

    public static IReadOnlyList<OnyxDocument> Documents => _documents.Value;

    public static JsonArray Search(OnyxSearchRequest request)
    {
        var terms = Terms(request.Message).Distinct(StringComparer.Ordinal).ToList();
        var hits = Documents
            .Where(d => (request.SourceTypes is null || request.SourceTypes.Contains(d.SourceType, StringComparer.Ordinal))
                && (request.TimeCutoff is null || d.UpdatedAt >= request.TimeCutoff))
            .Select(d => (Document: d, Score: Score(d, terms, out var chunk), Chunk: chunk))
            .Where(h => h.Score > 0)
            .OrderByDescending(h => h.Score)
            .ThenByDescending(h => h.Document.UpdatedAt)
            .ThenBy(h => h.Document.Id, StringComparer.Ordinal)
            .Skip(request.Offset)
            .Take(request.Limit)
            .Select(h => Hit(h.Document, h.Score, h.Chunk, terms));
        return [.. hits];
    }

    public static IEnumerable<string> Terms(string text) =>
        new string([.. text.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ')])
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1 && !_stopWords.Contains(t));

    private static double Score(OnyxDocument document, List<string> terms, out int bestChunk)
    {
        var counts = document.Chunks.Select(c => Terms(c).Count(terms.Contains)).ToList();
        bestChunk = counts.IndexOf(counts.Max());
        var words = Terms(document.Title).Concat(document.Chunks.SelectMany(Terms)).ToHashSet(StringComparer.Ordinal);
        var matched = terms.Count(words.Contains);
        return matched == 0 ? 0 : Math.Round(((double)matched / terms.Count) + (counts.Sum() * 0.001), 4, MidpointRounding.AwayFromZero);
    }

    private static JsonObject Hit(OnyxDocument document, double score, int chunk, List<string> terms)
    {
        var hit = (JsonObject)document.SearchDoc.DeepClone();
        hit["chunk_ind"] = chunk;
        hit["score"] = score;
        hit["is_relevant"] = null;
        hit["relevance_explanation"] = null;
        hit["match_highlights"] = new JsonArray([.. Highlights(document.Chunks[chunk], terms)]);
        return hit;
    }

    private static IEnumerable<JsonNode?> Highlights(string chunk, List<string> terms) =>
        chunk.Split(". ")
            .Where(sentence => Terms(sentence).Any(terms.Contains))
            .Take(2)
            .Select(sentence => JsonValue.Create(sentence));

    private static List<OnyxDocument> Load()
    {
        var file = JsonNode.Parse(SplitPanesFixture.ReadText("onyx/documents.json"));
        return [.. (file?["documents"]?.AsArray() ?? []).OfType<JsonObject>().Select(Document)];
    }

    private static OnyxDocument Document(JsonObject entry)
    {
        var doc = entry["search_doc"]?.AsObject() ?? throw new InvalidOperationException("onyx/documents.json: an entry has no search_doc.");
        return new OnyxDocument(
            Id: (string?)doc["document_id"] ?? throw new InvalidOperationException("onyx/documents.json: a search_doc has no document_id."),
            Title: (string?)doc["semantic_identifier"] ?? "",
            SourceType: (string?)doc["source_type"] ?? "",
            UpdatedAt: DateTimeOffset.Parse((string?)doc["updated_at"] ?? "", CultureInfo.InvariantCulture),
            SearchDoc: doc,
            Chunks: [.. (entry["chunks"]?.AsArray() ?? []).Select(c => (string?)c ?? "")]);
    }
}
