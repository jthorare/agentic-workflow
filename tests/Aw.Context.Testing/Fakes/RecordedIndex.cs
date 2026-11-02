using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Context.Testing.Fixtures;

namespace Aw.Context.Testing.Fakes;

/// <summary>
/// The recorded documents of the split-panes fixture ('recorded/documents.json': each a SearchDoc and its chunk texts). The recordings are
/// kept as data: LinearIssueSeed stores the Linear ones as the feed writes them.
/// </summary>
internal static class RecordedIndex
{
    private static readonly Lazy<List<RecordedDocument>> _documents = new(Load);

    public static IReadOnlyList<RecordedDocument> Documents => _documents.Value;

    private static List<RecordedDocument> Load()
    {
        var file = JsonNode.Parse(SplitPanesFixture.ReadText("recorded/documents.json"));
        return [.. (file?["documents"]?.AsArray() ?? []).OfType<JsonObject>().Select(Document)];
    }

    private static RecordedDocument Document(JsonObject entry)
    {
        var doc = entry["search_doc"]?.AsObject() ?? throw new InvalidOperationException("recorded/documents.json: an entry has no search_doc.");
        return new RecordedDocument(
            Id: (string?)doc["document_id"] ?? throw new InvalidOperationException("recorded/documents.json: a search_doc has no document_id."),
            Title: (string?)doc["semantic_identifier"] ?? "",
            SourceType: (string?)doc["source_type"] ?? "",
            UpdatedAt: DateTimeOffset.Parse((string?)doc["updated_at"] ?? "", CultureInfo.InvariantCulture),
            SearchDoc: doc,
            Chunks: [.. (entry["chunks"]?.AsArray() ?? []).Select(c => (string?)c ?? "")]);
    }
}
