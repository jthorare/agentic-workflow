using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.Records;

/// <summary>The library's rows in context.db (migration 0031_s1_review_library), read through the live records they belong to.</summary>
internal sealed class SqliteLibraryStore(ContextDatabase database) : ILibraryStore
{
    private static readonly string[] _refKinds = ["verifies", "relates", "blocks"];
    private static readonly string[] _itemKinds = ["section", "phase", "requirement", "acceptance", "testPlan", "task"];

    private static readonly IRowMapper<LibraryDoc> _doc = new Mapper<LibraryDoc>(r => new LibraryDoc(
        RecordKey.From(r.GetString(0)), r.GetString(1), r.GetString(2), Text(r, 3), Text(r, 4), Text(r, 5), Text(r, 6),
        DesignDocJson.ReadStrings(r.GetString(7)), DesignDocJson.ReadStrings(r.GetString(8)), r.GetInt32(9), DesignDocJson.ReadErrors(r.GetString(10)),
        r.GetString(11), Text(r, 12), Text(r, 13) is { } audience ? AudienceKey.From(audience) : null, LinearRowReads.At(r, 14)));

    private static readonly IRowMapper<LibraryItem> _item = new Mapper<LibraryItem>(r => new LibraryItem(
        RecordKey.From(r.GetString(0)), r.GetString(1), (DesignItemKind)Math.Max(0, Array.IndexOf(_itemKinds, r.GetString(2))), Text(r, 3), r.GetInt32(4), r.GetInt32(5), r.GetString(6)));

    private static readonly IRowMapper<ScopeLink> _scopeLink = new Mapper<ScopeLink>(r => new ScopeLink(RecordKey.From(r.GetString(0)), Text(r, 1), Text(r, 2)));

    // Item lines and text are not stored for the scope checks; a ref stores its line (0 for a row written before migration 0034).
    private static readonly IRowMapper<ScopeItem> _scopeItem = new Mapper<ScopeItem>(r => new ScopeItem(
        RecordKey.From(r.GetString(0)),
        new DesignItem(r.GetString(1), (DesignItemKind)Math.Max(0, Array.IndexOf(_itemKinds, r.GetString(2))), Text(r, 3), LineStart: 0, LineEnd: 0, Text: string.Empty)));

    private static readonly IRowMapper<ScopeRef> _scopeRef = new Mapper<ScopeRef>(r => new ScopeRef(
        RecordKey.From(r.GetString(0)),
        new DesignRef(Text(r, 1), r.GetString(2), (DesignRefKind)Math.Max(0, Array.IndexOf(_refKinds, r.GetString(3))), Line: (int)r.GetInt64(4))));

    private static readonly IRowMapper<LibraryClaimRow> _claim = new Mapper<LibraryClaimRow>(r => new LibraryClaimRow(
        RecordKey.From(r.GetString(0)), Text(r, 1) is { } audience ? AudienceKey.From(audience) : null, Text(r, 2) is { } state ? RecordWire.State(state) : null, r.GetString(3)));

    public ValueTask<Result<IReadOnlyList<LibraryDoc>>> Docs(RecordAudience mask, string? slug, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mask);
        var audiences = SqliteLexicalLeg.JsonArray(mask.Allowed.Select(static k => k.Value));
        var records = SqliteLexicalLeg.JsonArray(mask.Records.Select(static k => k.Value));
        return Query(new SqlStatement(LibrarySql.Docs, c => c.Bind("@slug", slug).Bind("@audiences", audiences).Bind("@records", records)), _doc, cancellationToken);
    }

    public async ValueTask<Result<LibraryDoc?>> Doc(RecordKey key, CancellationToken cancellationToken)
    {
        var rows = await Query(new SqlStatement(LibrarySql.Doc, c => c.Bind("@key", key.Value)), _doc, cancellationToken).ConfigureAwait(false);
        return rows.IsError ? rows.Error : Results.Success(rows.Value.Count == 0 ? null : rows.Value[0]);
    }

    public ValueTask<Result<IReadOnlyList<LibraryItem>>> Items(RecordKey doc, CancellationToken cancellationToken) =>
        Query(new SqlStatement(LibrarySql.Items, c => c.Bind("@key", doc.Value)), _item, cancellationToken);

    public async ValueTask<Result<IReadOnlyList<LibraryScopeFacts>>> ScopeFacts(IReadOnlyList<RecordKey> docs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(docs);
        var keys = SqliteLexicalLeg.JsonArray(docs.Select(static k => k.Value));
        var links = await Query(new SqlStatement(LibrarySql.ScopeLinks, c => c.Bind("@keys", keys)), _scopeLink, cancellationToken).ConfigureAwait(false);
        if (links.IsError)
        {
            return links.Error;
        }

        var items = await Query(new SqlStatement(LibrarySql.ScopeItems, c => c.Bind("@keys", keys)), _scopeItem, cancellationToken).ConfigureAwait(false);
        if (items.IsError)
        {
            return items.Error;
        }

        var refs = await Query(new SqlStatement(LibrarySql.ScopeRefs, c => c.Bind("@keys", keys)), _scopeRef, cancellationToken).ConfigureAwait(false);
        if (refs.IsError)
        {
            return refs.Error;
        }

        var itemsByDoc = items.Value.ToLookup(static i => i.Doc);
        var refsByDoc = refs.Value.ToLookup(static r => r.Doc);
        return Results.Success<IReadOnlyList<LibraryScopeFacts>>([.. links.Value
            .OrderBy(static l => l.Doc.Value, StringComparer.Ordinal)
            .Select(l => new LibraryScopeFacts(l.Doc, l.Parent, l.Supersedes, [.. itemsByDoc[l.Doc].Select(static i => i.Item)], [.. refsByDoc[l.Doc].Select(static r => r.Ref)]))]);
    }

    public ValueTask<Result<IReadOnlyList<LibraryClaimRow>>> ClaimsOf(RecordKey pr, CancellationToken cancellationToken) =>
        Query(new SqlStatement(LibrarySql.ClaimsOf, c => c.Bind("@key", pr.Value)), _claim, cancellationToken);

    public ValueTask<Result<IReadOnlyList<LibraryClaimRow>>> ClaimsOfMany(IReadOnlyList<RecordKey> prs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prs);
        var keys = SqliteLexicalLeg.JsonArray(prs.Select(static k => k.Value));
        return Query(new SqlStatement(LibrarySql.ClaimsOfMany, c => c.Bind("@keys", keys)), _claim, cancellationToken);
    }

    public ValueTask<Result<IReadOnlyList<LibraryClaimRow>>> ClaimsNaming(string slug, CancellationToken cancellationToken) =>
        Query(new SqlStatement(LibrarySql.ClaimsNaming, c => c.Bind("@slug", slug)), _claim, cancellationToken);

    // A store that was never created has no rows, and a read never creates it.
    private async ValueTask<Result<IReadOnlyList<T>>> Query<T>(SqlStatement statement, IRowMapper<T> mapper, CancellationToken cancellationToken)
    {
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError || readable.Value is not { } sessions)
        {
            return readable.IsError ? readable.Error : Results.Success<IReadOnlyList<T>>([]);
        }

        var read = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ContextDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(read.Value, statement, mapper, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows;
        }
    }

    private static string? Text(SqliteDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : r.GetString(ordinal);

    private sealed record ScopeLink(RecordKey Doc, string? Parent, string? Supersedes);

    private sealed record ScopeItem(RecordKey Doc, DesignItem Item);

    private sealed record ScopeRef(RecordKey Doc, DesignRef Ref);

    private sealed class Mapper<T>(Func<SqliteDataReader, T> map) : IRowMapper<T>
    {
        public T Map(SqliteDataReader reader) => map(reader);
    }
}
