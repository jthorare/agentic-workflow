namespace Aw.Context.Infrastructure.Docs;

/// <summary>The repo-docs SQL (context.db, migrations 0001_s1a_repo_docs and 0003_s2w_scrub_ruleset). Engine-specific parts (FTS5, bm25, json_each, upserts) stay here.</summary>
internal static class DocSql
{
    internal const string Source = "repo-docs";

    internal const string StoredDocs = "SELECT path, blob, ruleset FROM doc WHERE repo = @repo";

    internal const string Texts =
        "SELECT d.path, d.blob, d.blocked, c.text FROM doc d LEFT JOIN doc_chunk c ON c.repo = d.repo AND c.path = d.path " +
        "WHERE d.repo = @repo ORDER BY d.path, c.line_start";

    internal const string DeleteChunks = "DELETE FROM doc_chunk WHERE repo = @repo AND path = @path";

    internal const string DeleteDoc = "DELETE FROM doc WHERE repo = @repo AND path = @path";

    internal const string InsertDoc = "INSERT INTO doc (repo, path, blob, title, blocked, ruleset) VALUES (@repo, @path, @blob, @title, @blocked, @ruleset)";

    internal const string InsertChunk =
        "INSERT INTO doc_chunk (repo, path, blob, line_start, line_end, text) VALUES (@repo, @path, @blob, @start, @end, @text)";

    internal const string Synced =
        "INSERT INTO source_cursor (source, repo, cursor, last_sync, last_error) VALUES (@source, @repo, @cursor, @at, NULL) " +
        "ON CONFLICT (source, repo) DO UPDATE SET cursor = excluded.cursor, last_sync = excluded.last_sync, last_error = NULL";

    internal const string Failed =
        "INSERT INTO source_cursor (source, repo, cursor, last_sync, last_error) VALUES (@source, @repo, NULL, NULL, @code) " +
        "ON CONFLICT (source, repo) DO UPDATE SET last_error = excluded.last_error";

    internal const string Status =
        "SELECT (SELECT cursor FROM source_cursor WHERE source = @source AND repo = @repo), " +
        "(SELECT last_sync FROM source_cursor WHERE source = @source AND repo = @repo), " +
        "(SELECT last_error FROM source_cursor WHERE source = @source AND repo = @repo), " +
        "(SELECT count(*) FROM doc WHERE repo = @repo), " +
        "(SELECT count(*) FROM doc_chunk WHERE repo = @repo), " +
        "(SELECT count(*) FROM doc WHERE repo = @repo AND ruleset <> @ruleset)";

    internal const string Search =
        "SELECT c.repo, c.path, c.blob, c.line_start, c.line_end, d.title " +
        "FROM doc_chunk_fts f " +
        "JOIN doc_chunk c ON c.id = f.rowid " +
        "JOIN doc d ON d.repo = c.repo AND d.path = c.path " +
        "WHERE doc_chunk_fts MATCH @match AND c.repo IN (SELECT value FROM json_each(@repos)) AND d.ruleset = @ruleset " +
        "ORDER BY bm25(doc_chunk_fts), c.id " +
        "LIMIT @limit";

    /// <summary>One term as an FTS5 phrase: quoted, inner quotes doubled, so it is never read as query syntax (spec §5.1).</summary>
    /// <param name="term">The term.</param>
    /// <returns>The phrase.</returns>
    internal static string Phrase(string term) => "\"" + term.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
