using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Records;

internal sealed partial class SqliteRecordStore
{
    private static readonly string[] _itemKinds = ["section", "phase", "requirement", "acceptance", "testPlan", "task"];
    private static readonly string[] _refKinds = ["verifies", "relates", "blocks"];

    // A record write replaces its design-doc rows or claims (spec 2026-11-04 §5.3) in the page's transaction. A doc record with no parse is no design doc (or no longer one), and a PR with no claims list claims nothing,
    // so both clear what an earlier version of the record left.
    private static async ValueTask<Error?> WriteLibraryRows(SqliteUnitOfWork session, RecordWrite write, CancellationToken cancellationToken)
    {
        var record = write.Record;
        if (record.Kind == RecordKind.Doc)
        {
            return await ReplaceDoc(session, record, write.Doc, cancellationToken).ConfigureAwait(false);
        }

        return record.Kind == RecordKind.Pr ? await ReplaceClaims(session, record, write.Claims ?? [], cancellationToken).ConfigureAwait(false) : null;
    }

    // A removed record keeps its row but not its items, refs or claims.
    private static async ValueTask<Error?> DeleteLibraryRows(SqliteUnitOfWork session, string key, CancellationToken cancellationToken) =>
        await DeleteDoc(session, key, cancellationToken).ConfigureAwait(false)
            ?? await Exec(session, DesignDocSql.DeleteClaims, c => c.Bind("@key", key), cancellationToken).ConfigureAwait(false);

    private static async ValueTask<Error?> DeleteDoc(SqliteUnitOfWork session, string key, CancellationToken cancellationToken) =>
        await Exec(session, DesignDocSql.DeleteRefs, c => c.Bind("@key", key), cancellationToken).ConfigureAwait(false)
            ?? await Exec(session, DesignDocSql.DeleteItems, c => c.Bind("@key", key), cancellationToken).ConfigureAwait(false)
            ?? await Exec(session, DesignDocSql.DeleteDoc, c => c.Bind("@key", key), cancellationToken).ConfigureAwait(false);

    private static async ValueTask<Error?> ReplaceDoc(SqliteUnitOfWork session, StoredRecord record, ParsedDesignDoc? doc, CancellationToken cancellationToken)
    {
        var key = record.Key.Value;
        var failure = await DeleteDoc(session, key, cancellationToken).ConfigureAwait(false);
        return failure is not null || doc is null
            ? failure
            : await InsertDoc(session, key, doc, cancellationToken).ConfigureAwait(false)
                ?? await InsertItems(session, key, doc, cancellationToken).ConfigureAwait(false)
                ?? await InsertRefs(session, key, doc, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<Error?> InsertDoc(SqliteUnitOfWork session, string key, ParsedDesignDoc doc, CancellationToken cancellationToken)
    {
        var (root, scope) = RootAndScope(key);
        var fm = doc.FrontMatter;
        return await Exec(session, DesignDocSql.InsertDoc, c => c
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
            .Bind("@errors", DesignDocJson.Errors(doc.Errors)), cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<Error?> InsertItems(SqliteUnitOfWork session, string key, ParsedDesignDoc doc, CancellationToken cancellationToken)
    {
        foreach (var item in doc.Items)
        {
            var written = await Exec(session, DesignDocSql.InsertItem, c => c
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

    private static async ValueTask<Error?> InsertRefs(SqliteUnitOfWork session, string key, ParsedDesignDoc doc, CancellationToken cancellationToken)
    {
        foreach (var link in doc.Refs)
        {
            var written = await Exec(session, DesignDocSql.InsertRef, c => c
                .Bind("@key", key)
                .Bind("@from", link.From)
                .Bind("@to", link.To)
                .Bind("@kind", _refKinds[(int)link.Kind]), cancellationToken).ConfigureAwait(false);
            if (written is not null)
            {
                return written;
            }
        }

        return null;
    }

    private static async ValueTask<Error?> ReplaceClaims(SqliteUnitOfWork session, StoredRecord record, IReadOnlyList<string> claims, CancellationToken cancellationToken)
    {
        var key = record.Key.Value;
        var failure = await Exec(session, DesignDocSql.DeleteClaims, c => c.Bind("@key", key), cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        foreach (var claim in claims)
        {
            failure = await Exec(session, DesignDocSql.InsertClaim, c => c.Bind("@key", key).Bind("@ref", claim), cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
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
