using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Records;

/// <summary>The writer of <see cref="DesignDocSideRows"/> (spec 2026-11-04 §5.3): <c language="text">design_doc</c>, its items and its refs, migration 0031, its parent and supersedes, migration 0033, and its refs' lines, migration 0034.</summary>
internal sealed class DesignDocRowWriter : ISideRowWriter
{
    private static readonly string[] _itemKinds = ["section", "phase", "requirement", "acceptance", "testPlan", "task"];
    private static readonly string[] _refKinds = ["verifies", "relates", "blocks"];

    public string Id => DesignDocSideRows.WriterId;

    // Differs when the doc's header is missing, was parsed by another parser version (0033's parent and supersedes columns come with version 2, so a doc stored under 1 is rewritten), or holds another number of items; the record's own version and ruleset already cover a changed text.
    public async ValueTask<Result<bool>> Differs(ISqliteSession session, RecordKey key, SideRows rows, CancellationToken cancellationToken)
    {
        var doc = ((DesignDocSideRows)rows).Doc;
        var versions = await SqliteQueries.List(session, new SqlStatement(DesignDocSql.StoredVersion, c => c.Bind("@key", key.Value)), SideRowReads.Number, cancellationToken).ConfigureAwait(false);
        if (versions.IsError)
        {
            return ContextDatabase.Translate(versions.Error);
        }

        if (doc is null || versions.Value.Count == 0)
        {
            return (doc is null) != (versions.Value.Count == 0);
        }

        var items = await SqliteQueries.List(session, new SqlStatement(DesignDocSql.StoredItemCount, c => c.Bind("@key", key.Value)), SideRowReads.Number, cancellationToken).ConfigureAwait(false);
        return items.IsError ? ContextDatabase.Translate(items.Error) : versions.Value[0] != doc.ParserVersion || items.Value[0] != doc.Items.Count;
    }

    public async ValueTask<Error?> Replace(ISqliteSession session, RecordKey key, SideRows rows, bool newRecord, CancellationToken cancellationToken)
    {
        var failure = await Delete(session, key, cancellationToken).ConfigureAwait(false);
        return failure is not null || ((DesignDocSideRows)rows).Doc is not { } doc
            ? failure
            : await InsertDoc(session, key.Value, doc, cancellationToken).ConfigureAwait(false)
                ?? await InsertItems(session, key.Value, doc, cancellationToken).ConfigureAwait(false)
                ?? await InsertRefs(session, key.Value, doc, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Error?> Delete(ISqliteSession session, RecordKey key, CancellationToken cancellationToken) =>
        await SqliteRecordStore.Exec(session, DesignDocSql.DeleteRefs, c => c.Bind("@key", key.Value), cancellationToken).ConfigureAwait(false)
            ?? await SqliteRecordStore.Exec(session, DesignDocSql.DeleteItems, c => c.Bind("@key", key.Value), cancellationToken).ConfigureAwait(false)
            ?? await SqliteRecordStore.Exec(session, DesignDocSql.DeleteDoc, c => c.Bind("@key", key.Value), cancellationToken).ConfigureAwait(false);

    private static async ValueTask<Error?> InsertDoc(ISqliteSession session, string key, ParsedDesignDoc doc, CancellationToken cancellationToken)
    {
        var (root, scope) = RootAndScope(key);
        var fm = doc.FrontMatter;
        return await SqliteRecordStore.Exec(session, DesignDocSql.InsertDoc, c => c
            .Bind("@key", key)
            .Bind("@root", root)
            .Bind("@scope", scope)
            .Bind("@docId", fm?.Slug)
            .Bind("@kind", fm?.Kind)
            .Bind("@status", fm?.Status)
            .Bind("@project", fm?.Project)
            .Bind("@repos", DesignDocJson.Strings(fm?.Repos))
            .Bind("@issues", DesignDocJson.Strings(fm?.Issues))
            .Bind("@version", doc.ParserVersion)
            .Bind("@errors", DesignDocJson.Errors(doc.Errors))
            .Bind("@parent", fm?.Parent)
            .Bind("@supersedes", fm?.Supersedes), cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<Error?> InsertItems(ISqliteSession session, string key, ParsedDesignDoc doc, CancellationToken cancellationToken)
    {
        foreach (var item in doc.Items)
        {
            var written = await SqliteRecordStore.Exec(session, DesignDocSql.InsertItem, c => c
                .Bind("@key", key)
                .Bind("@item", item.Key)
                .Bind("@kind", _itemKinds[(int)item.Kind])
                .Bind("@parent", item.Parent)
                .Bind("@lineStart", item.LineStart)
                .Bind("@lineEnd", item.LineEnd)
                .Bind("@text", Capped(item.Text)), cancellationToken).ConfigureAwait(false);
            if (written is not null)
            {
                return written;
            }
        }

        return null;
    }

    private static async ValueTask<Error?> InsertRefs(ISqliteSession session, string key, ParsedDesignDoc doc, CancellationToken cancellationToken)
    {
        foreach (var link in doc.Refs)
        {
            var written = await SqliteRecordStore.Exec(session, DesignDocSql.InsertRef, c => c
                .Bind("@key", key)
                .Bind("@from", link.From)
                .Bind("@to", link.To)
                .Bind("@kind", _refKinds[(int)link.Kind])
                .Bind("@line", link.Line), cancellationToken).ConfigureAwait(false);
            if (written is not null)
            {
                return written;
            }
        }

        return null;
    }

    // why: the table's CHECK is 4,000 characters; the parser already caps, this keeps a caller's own parse from failing a whole page
    private static string Capped(string text) => text.Length <= DesignDocParser.MaxTextChars ? text : text[..DesignDocParser.MaxTextChars];

    // 'repo:<name>/<path>': root and scope are 'repo:<name>'. 'doc:<root>/...': the root; in the 'aw' root the scope adds the repo slug ('aw/<slug>'), elsewhere it is the root (spec §4.2).
    private static (string Root, string Scope) RootAndScope(string key)
    {
        if (key.StartsWith("repo:", StringComparison.Ordinal))
        {
            var slash = key.IndexOf('/', StringComparison.Ordinal);
            var root = slash > 0 ? key[..slash] : key;
            return (root, root);
        }

        var parts = key.StartsWith("doc:", StringComparison.Ordinal) ? key["doc:".Length..].Split('/') : [key];
        return parts[0] == "aw" && parts.Length > 2 ? ("aw", $"aw/{parts[1]}") : (parts[0], parts[0]);
    }
}
