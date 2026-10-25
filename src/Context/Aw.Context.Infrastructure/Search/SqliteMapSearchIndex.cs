using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Search;
using Aw.Kernel;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.Search;

#pragma warning disable CA2100 // every command text is a constant or built from the two fixed table names and weight lists above; the question and the area go in parameters

/// <summary>
/// One map's full-text index, in memory (an FTS5 table per map version and caller scope, rebuilt when the graph a reader hands back changes; nothing is written to disk). Two tables share the document ids:
/// 'doc' (unicode61 words, so "split pane" finds "Split pane" and the last word matches as a prefix) ranked by bm25 with title over path over text, and 'tri' (trigram over title and path) that answers
/// when no whole word matched, so "plit" or "abManag" still finds "Tab manager". One connection serves every question, under a lock.
/// </summary>
internal sealed class SqliteMapSearchIndex : IMapSearchIndex, IDisposable
{
    private const string Words = "doc";
    private const string Trigrams = "tri";
    private const int TrigramMinimum = 3;

    private static readonly string[] _parameters = ["$id", "$title", "$path", "$area"];

    private readonly SqliteConnection _db;
    private readonly Lock _gate = new();

    private SqliteMapSearchIndex(SqliteConnection db)
    {
        _db = db;
    }

    /// <summary>Indexes <paramref name="docs"/> (ids are their rowids).</summary>
    internal static Result<SqliteMapSearchIndex> Create(IReadOnlyList<MapSearchDoc> docs)
    {
#pragma warning disable CA2000 // disposed on the failure path; on success the index owns it
        var db = new SqliteConnection("Data Source=:memory:");
#pragma warning restore CA2000
        try
        {
            db.Open();
            Exec(db, "CREATE VIRTUAL TABLE doc USING fts5(title, path, text, area UNINDEXED, ident UNINDEXED, tokenize = 'unicode61 remove_diacritics 2', prefix = '2 3')");
            Exec(db, "CREATE VIRTUAL TABLE tri USING fts5(title, path, area UNINDEXED, ident UNINDEXED, tokenize = 'trigram')");
            using var transaction = db.BeginTransaction();
            using var words = Insert(db, transaction, "INSERT INTO doc(rowid, title, path, text, area, ident) VALUES ($id, $title, $path, $text, $area, $id)", text: "$text");
            using var trigrams = Insert(db, transaction, "INSERT INTO tri(rowid, title, path, area, ident) VALUES ($id, $title, $path, $area, $id)", text: null);
            foreach (var doc in docs)
            {
                Fill(words, doc);
                _ = words.ExecuteNonQuery();
                Fill(trigrams, doc);
                _ = trigrams.ExecuteNonQuery();
            }

            transaction.Commit();
            return new SqliteMapSearchIndex(db);
        }
        catch (SqliteException e)
        {
            db.Dispose();
            return Unavailable(e);
        }
    }

    public Result<MapSearchPage> Search(IReadOnlyList<string> terms, string? area, int limit)
    {
        ArgumentNullException.ThrowIfNull(terms);
        try
        {
            lock (_gate)
            {
                var page = Run(Words, WordExpression(terms), area, limit);
                return page.Total > 0 || TrigramExpression(terms) is not { } fallback ? page : Run(Trigrams, fallback, area, limit);
            }
        }
        catch (SqliteException e)
        {
            return Unavailable(e);
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _db.Dispose();

    // Every word quoted (they hold only letters and digits), the last also a prefix: "tab" "manag"*.
    private static string WordExpression(IReadOnlyList<string> terms) =>
        string.Join(' ', terms.Select((t, i) => i == terms.Count - 1 ? $"\"{t}\"*" : $"\"{t}\""));

    // The words long enough for a trigram, as substrings; null when none is.
    private static string? TrigramExpression(IReadOnlyList<string> terms)
    {
        var usable = terms.Where(static t => t.Length >= TrigramMinimum).ToList();
        return usable.Count == 0 ? null : string.Join(' ', usable.Select(static t => $"\"{t}\""));
    }

    // Ranked by bm25 rounded to 4 places (on a small map nearly every word is in more than half the documents, bm25 clamps those to ~0 and differences below that are noise), then the shorter title,
    // then the document id (key order); the id is stored in its own column and read back from it, so a hit never depends on the rowid: the same graph and question always give the same order.
    private MapSearchPage Run(string table, string match, string? area, int limit)
    {
        var weights = table == Words ? "10.0, 5.0, 1.0, 0.0, 0.0" : "10.0, 5.0, 0.0, 0.0";
        var filter = area is null ? string.Empty : " AND area = $area";
        using var count = _db.CreateCommand();
        count.CommandText = $"SELECT count(*) FROM {table} WHERE {table} MATCH $match{filter}";
        Bind(count, match, area);
        var total = Convert.ToInt32(count.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (total == 0)
        {
            return new MapSearchPage(0, []);
        }

        using var ask = _db.CreateCommand();
        ask.CommandText = $"SELECT ident, -bm25({table}, {weights}) AS score FROM {table} WHERE {table} MATCH $match{filter} ORDER BY round(bm25({table}, {weights}), 4), length(title), ident LIMIT $limit";
        Bind(ask, match, area);
        _ = ask.Parameters.AddWithValue("$limit", limit);
        var matches = new List<MapSearchMatch>();
        using var reader = ask.ExecuteReader();
        while (reader.Read())
        {
            matches.Add(new MapSearchMatch(reader.GetInt32(0), reader.GetDouble(1)));
        }

        return new MapSearchPage(total, matches);
    }

    private static void Bind(SqliteCommand command, string match, string? area)
    {
        _ = command.Parameters.AddWithValue("$match", match);
        if (area is not null)
        {
            _ = command.Parameters.AddWithValue("$area", area);
        }
    }

    private static SqliteCommand Insert(SqliteConnection db, SqliteTransaction transaction, string sql, string? text)
    {
        var command = db.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var name in _parameters.Concat(text is null ? [] : [text]))
        {
            _ = command.Parameters.Add(new SqliteParameter(name, value: null));
        }

        return command;
    }

    private static void Fill(SqliteCommand command, MapSearchDoc doc)
    {
        command.Parameters["$id"].Value = doc.Id;
        command.Parameters["$title"].Value = doc.Title;
        command.Parameters["$path"].Value = doc.Path;
        command.Parameters["$area"].Value = doc.Area;
        if (command.Parameters.Contains("$text"))
        {
            command.Parameters["$text"].Value = doc.Text;
        }
    }

    private static void Exec(SqliteConnection db, string sql)
    {
        using var command = db.CreateCommand();
        command.CommandText = sql;
        _ = command.ExecuteNonQuery();
    }

    private static Error Unavailable(SqliteException e) =>
        ContextErrors.StoreUnavailable.With(string.Create(CultureInfo.InvariantCulture, $"the map search index failed (SQLite {e.SqliteErrorCode}) (fix: retry; if it persists, the SQLite build may lack FTS5)"));
}
#pragma warning restore CA2100
