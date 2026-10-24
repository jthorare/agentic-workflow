namespace Aw.Sindri.Ui.Api.Context;

/// <summary>GET /sindri/context/filemap: one directory of a repo with its status marks.</summary>
/// <param name="SchemaVersion">SchemaVersion, as served.</param>
/// <param name="Repo">Repo, as served.</param>
/// <param name="Sha">Sha, as served.</param>
/// <param name="MapVersion">MapVersion, as served.</param>
/// <param name="Draft">Draft, as served.</param>
/// <param name="Path">Path, as served.</param>
/// <param name="Entries">Entries, as served.</param>
/// <param name="Unmapped">Unmapped, as served.</param>
/// <param name="Suggested">Suggested, as served.</param>
/// <param name="Totals">Totals, as served.</param>
/// <param name="Symbols">Symbols, as served.</param>
/// <param name="NextCursor">Where the next page starts, or null on the last page (and from a server that does not page).</param>
/// <param name="Total">The direct children there are in all, or null from a server that does not page.</param>
internal sealed record FileMapView(
    int SchemaVersion,
    string Repo,
    string Sha,
    string MapVersion,
    bool Draft,
    string Path,
    IReadOnlyList<FileMapEntry> Entries,
    IReadOnlyList<UnmappedPath> Unmapped,
    IReadOnlyList<FileSuggestion> Suggested,
    FileMapTotals Totals,
    SymbolsView Symbols,
    string? NextCursor = null,
    int? Total = null);
