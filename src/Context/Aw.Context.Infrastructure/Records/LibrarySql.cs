namespace Aw.Context.Infrastructure.Records;

/// <summary>The design-doc library's read SQL (migration 0031_s1_review_library). Values are always bound, never formatted in.</summary>
internal static class LibrarySql
{
    private const string DocColumns =
        "d.doc_key, d.root, d.scope, d.doc_id, d.kind, d.status, d.project, d.repos, d.issues, d.parser_version, d.errors, r.title, r.url, r.audience_key";

    // The superset mask (the lexical leg's): the record's audience is allowed, or the record is listed and is not an 'owner:' one. The service applies AudienceScope.Allows to what comes back.
    internal const string Docs =
        "SELECT " + DocColumns + " FROM design_doc d JOIN source_record r ON r.key = d.doc_key " +
        "WHERE r.removed_at IS NULL AND (@slug IS NULL OR d.doc_id = @slug) " +
        "AND (r.audience_key IN (SELECT value FROM json_each(@audiences)) OR (r.key IN (SELECT value FROM json_each(@records)) AND coalesce(r.audience_key, '') NOT LIKE 'owner:%')) " +
        "ORDER BY d.doc_key";

    internal const string Doc =
        "SELECT " + DocColumns + " FROM design_doc d JOIN source_record r ON r.key = d.doc_key WHERE d.doc_key = @key AND r.removed_at IS NULL";

    // Document order is insertion order: the writer inserts the parser's items as they come.
    internal const string Items =
        "SELECT i.doc_key, i.item_id, i.item_kind, i.parent_id, i.line_start, i.line_end, i.text FROM design_doc_item i " +
        "JOIN source_record r ON r.key = i.doc_key WHERE i.doc_key = @key AND r.removed_at IS NULL ORDER BY i.rowid";

    internal const string ClaimsOf =
        "SELECT c.pr_key, r.audience_key, r.state, c.ref FROM design_doc_claim c JOIN source_record r ON r.key = c.pr_key " +
        "WHERE c.pr_key = @key AND r.removed_at IS NULL ORDER BY c.ref";

    // doc_id is the doc half of the ref as written (design_doc_claim_by_doc_id); the service resolves each ref exactly.
    internal const string ClaimsNaming =
        "SELECT c.pr_key, r.audience_key, r.state, c.ref FROM design_doc_claim c JOIN source_record r ON r.key = c.pr_key " +
        "WHERE r.removed_at IS NULL AND c.doc_id = @slug ORDER BY c.pr_key, c.ref";
}
