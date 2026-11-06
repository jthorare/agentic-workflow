namespace Aw.Context.Infrastructure.Records;

/// <summary>The design-doc library's SQL (migration 0031_s1_review_library). Values are always bound, never formatted in.</summary>
internal static class DesignDocSql
{
    internal const string StoredVersion = "SELECT parser_version FROM design_doc WHERE doc_key = @key";

    internal const string StoredItemCount = "SELECT count(*) FROM design_doc_item WHERE doc_key = @key";

    internal const string StoredClaims = "SELECT ref FROM design_doc_claim WHERE pr_key = @key ORDER BY ref";

    internal const string DeleteRefs = "DELETE FROM design_doc_ref WHERE doc_key = @key";

    internal const string DeleteItems = "DELETE FROM design_doc_item WHERE doc_key = @key";

    internal const string DeleteDoc = "DELETE FROM design_doc WHERE doc_key = @key";

    internal const string InsertDoc =
        "INSERT INTO design_doc (doc_key, root, scope, doc_id, kind, status, project, repos, issues, parser_version, errors) " +
        "VALUES (@key, @root, @scope, @docId, @kind, @status, @project, @repos, @issues, @version, @errors)";

    internal const string InsertItem =
        "INSERT INTO design_doc_item (doc_key, item_id, item_kind, parent_id, line_start, line_end, text) VALUES (@key, @item, @kind, @parent, @lineStart, @lineEnd, @text)";

    internal const string InsertRef = "INSERT INTO design_doc_ref (doc_key, from_item, to_ref, kind) VALUES (@key, @from, @to, @kind)";

    internal const string DeleteClaims = "DELETE FROM design_doc_claim WHERE pr_key = @key";

    internal const string InsertClaim = "INSERT INTO design_doc_claim (pr_key, ref, doc_id, item_id) VALUES (@key, @ref, @doc, @item) ON CONFLICT DO NOTHING";
}
