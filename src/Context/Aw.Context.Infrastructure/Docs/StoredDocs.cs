namespace Aw.Context.Infrastructure.Docs;

/// <summary>A repo's stored documents by path, and what a refresh decides from them.</summary>
/// <param name="Docs">The stored documents.</param>
internal sealed record StoredDocs(IReadOnlyDictionary<string, StoredDoc> Docs)
{
    /// <summary>True when <paramref name="path"/> is stored at <paramref name="blob"/> under <paramref name="ruleset"/>: nothing to clean again.</summary>
    internal bool Current(string path, string blob, string ruleset) =>
        Docs.TryGetValue(path, out var doc) && string.Equals(doc.Blob, blob, StringComparison.Ordinal) && string.Equals(doc.Ruleset, ruleset, StringComparison.Ordinal);

    /// <summary>
    /// True when a prepared document's blob was already stored (so it was cleaned again only because the rules changed)
    /// and it was indexed with text: a blocked one is counted as skipped or blocked instead, so the
    /// re-cleaned documents are always among the added ones.
    /// </summary>
    internal bool Recleaned(PreparedDoc prepared) =>
        prepared is { Blocked: false } && Docs.TryGetValue(prepared.Path, out var doc) && string.Equals(doc.Blob, prepared.Blob, StringComparison.Ordinal);
}
