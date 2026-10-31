using System.Text;
using System.Text.Json;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;
using Microsoft.Data.Sqlite;

namespace Aw.CodeIndex.Infrastructure.Store;

internal sealed partial class SqliteIndexReader
{
    private const string FindExact = "SELECT " + HitColumns + " FROM symbols s WHERE s.id IN ( "
        + "SELECT id FROM symbols WHERE name = $text "
        + "UNION SELECT id FROM symbols WHERE ref = $text "
        + "UNION SELECT id FROM symbols WHERE file = $text "
        + "UNION SELECT id FROM symbols WHERE file = $path AND name = $name) "
        + "ORDER BY s.file, s.start_line, s.id LIMIT $limit";

    private const string FindPrefixOpen = "SELECT " + HitColumns + " FROM symbols s WHERE s.name >= $low ORDER BY s.file, s.start_line, s.id LIMIT $limit";

    private const string FindPrefix = "SELECT " + HitColumns + " FROM symbols s WHERE s.name >= $low AND s.name < $high ORDER BY s.file, s.start_line, s.id LIMIT $limit";

    // Within filters in SQL before the LIMIT, so the limit fills with in-set rows and an empty answer means none (concept pins spec §3.5).
    private static readonly string _findExactWithin = Narrowed(FindExact);

    private static readonly string _findPrefixOpenWithin = Narrowed(FindPrefixOpen);

    private static readonly string _findPrefixWithin = Narrowed(FindPrefix);

    private const string InFile = "SELECT " + HitColumns + " FROM symbols s WHERE s.file = $path ORDER BY s.start_line, s.id";

    public ValueTask<Result<SymbolPage>> FindSymbols(SymbolQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return SqliteFailure.Guard("Finding symbols", async () => await Find(query, cancellationToken).ConfigureAwait(false));
    }

    public ValueTask<Result<IReadOnlyList<FileSymbolsEntry>>> FileSymbols(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return SqliteFailure.Guard("Listing a file's symbols", async () => await InFiles(paths, cancellationToken).ConfigureAwait(false));
    }

    private async ValueTask<Result<SymbolPage>> Find(SymbolQuery query, CancellationToken cancellationToken)
    {
        if (query.Text.Length == 0 || query.Limit < 1)
        {
            return CodeIndexErrors.QueryInvalid.With("A symbol query needs some text and a limit of at least 1.");
        }

        var limit = query.Limit;
        var within = query.Within is { } files ? JsonSerializer.Serialize(files.Select(static f => f.Path).Distinct(StringComparer.Ordinal).ToArray(), WithinWire.Options) : null;
        var command = query.Match == SymbolMatch.Prefix ? PrefixCommand(query.Text, within is not null) : ExactCommand(query.Text, within is not null);
        await using (command.ConfigureAwait(false))
        {
            _ = command.With("$limit", limit + 1);
            if (within is not null)
            {
                _ = command.With("$within", within);
            }

            var hits = await ReadHits(command, cancellationToken).ConfigureAwait(false);
            return new SymbolPage([.. hits.Take(limit)], hits.Count > limit);
        }
    }

    private static string Narrowed(string sql) => sql.Replace(" ORDER BY", " AND s.file IN (SELECT value FROM json_each($within)) ORDER BY", StringComparison.Ordinal);

    private SqliteCommand ExactCommand(string text, bool within)
    {
        var split = text.LastIndexOf('#');
        return Sql.Command(connection, within ? _findExactWithin : FindExact)
            .With("$text", text)
            .With("$path", split > 0 ? text[..split] : null)
            .With("$name", split > 0 ? text[(split + 1)..] : null);
    }

    private SqliteCommand PrefixCommand(string text, bool within)
    {
        var high = PrefixUpperBound(text);
        var command = Sql.Command(connection, high is null ? (within ? _findPrefixOpenWithin : FindPrefixOpen) : (within ? _findPrefixWithin : FindPrefix)).With("$low", text);
        return high is null ? command : command.With("$high", high);
    }

    private async Task<Result<IReadOnlyList<FileSymbolsEntry>>> InFiles(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var entries = new List<FileSymbolsEntry>(paths.Count);
        foreach (var path in paths)
        {
            var command = Sql.Command(connection, InFile).With("$path", path);
            await using (command.ConfigureAwait(false))
            {
                entries.Add(new FileSymbolsEntry(path, await ReadHits(command, cancellationToken).ConfigureAwait(false)));
            }
        }

        return entries;
    }

    private static async Task<List<SymbolHit>> ReadHits(SqliteCommand command, CancellationToken cancellationToken)
    {
        var hits = new List<SymbolHit>();
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                hits.Add(ReadHit(reader, 0));
            }
        }

        return hits;
    }

    /// <summary>The smallest text above every text that starts with <paramref name="prefix"/>, in the code point order SQLite sorts UTF-8 by; null when none exists.</summary>
    private static string? PrefixUpperBound(string prefix)
    {
        var runes = prefix.EnumerateRunes().ToList();
        for (var i = runes.Count - 1; i >= 0; i--)
        {
            var next = runes[i].Value + 1;
            if (next is >= 0xD800 and <= 0xDFFF)
            {
                next = 0xE000;
            }

            if (next <= 0x10FFFF)
            {
                var bound = new StringBuilder();
                foreach (var rune in runes.Take(i))
                {
                    _ = bound.Append(rune.ToString());
                }

                return bound.Append(new Rune(next).ToString()).ToString();
            }
        }

        return null;
    }
}
