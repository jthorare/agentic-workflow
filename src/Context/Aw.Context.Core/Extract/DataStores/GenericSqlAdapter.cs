using System.Text.RegularExpressions;

namespace Aw.Context.Core.Extract.DataStores;

/// <summary>
/// Plain SQL: CREATE TABLE in schema files, and in code the SQL text (SELECT ... FROM / JOIN, INSERT INTO, UPDATE ... SET, DELETE FROM) or
/// a query-builder call naming the table as a string literal (.from('t'), .table('t'), .into('t')) followed in the same statement by a read
/// call (select, where, first, pluck, count) or a write call (insert, update, delete, del, upsert); a write call wins over a where. A table
/// named only in a comment or a log text is no use.
/// </summary>
public sealed partial class GenericSqlAdapter : IDataStoreAdapter
{
    private const string Qualified = "(?:[`\"\\[]?[\\w$]+[`\"\\]]?\\.)*[`\"\\[]?(?<t>[\\w$]+)[`\"\\]]?";
    private const int LookBack = 500;
    private const int ChainLength = 400;

    /// <inheritdoc />
    public string Name => "sql";

    /// <inheritdoc />
    public IEnumerable<string> Declared(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Safe<string>(() => [.. CreateTable.Matches(text).Select(m => m.Groups["t"].Value)]);
    }

    /// <inheritdoc />
    public IEnumerable<StoreAccess> Accesses(string text, IReadOnlySet<string> stores)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(stores);
        return Safe<StoreAccess>(() => [.. SqlReads(text).Concat(SqlWrites(text)).Concat(Builder(text))
            .Where(a => stores.Contains(a.Store.ToLowerInvariant()))
            .DistinctBy(a => (a.Store.ToLowerInvariant(), a.Access))]);
    }

    // A pattern that ran out of time on one file leaves that file with no finds, never a failed build.
    private static List<T> Safe<T>(Func<List<T>> find)
    {
        try
        {
            return find();
        }
        catch (RegexMatchTimeoutException)
        {
            return [];
        }
    }

    // FROM and JOIN only count with a SELECT before them in the same statement (the prose "read from shared_link" is no query); DELETE FROM is a write.
    private static IEnumerable<StoreAccess> SqlReads(string text)
    {
        foreach (Match m in FromOrJoin.Matches(text))
        {
            var start = Math.Max(m.Index - LookBack, 0);
            var window = text[start..m.Index];
            var cut = window.LastIndexOf(';');
            window = cut < 0 ? window : window[(cut + 1)..];
            if (SelectWord.IsMatch(window) && !DeleteBefore.IsMatch(window))
            {
                yield return new StoreAccess(m.Groups["t"].Value, DataAccess.Read, m.Value);
            }
        }
    }

    private static IEnumerable<StoreAccess> SqlWrites(string text) =>
        InsertInto.Matches(text).Concat(UpdateSet.Matches(text)).Concat(DeleteFrom.Matches(text))
            .Select(m => new StoreAccess(m.Groups["t"].Value, DataAccess.Write, m.Value));

    private static IEnumerable<StoreAccess> Builder(string text)
    {
        foreach (Match m in BuilderTable.Matches(text))
        {
            var rest = text[(m.Index + m.Length)..];
            rest = rest[..Math.Min(rest.Length, ChainLength)];
            var end = rest.IndexOf(';', StringComparison.Ordinal);
            rest = end < 0 ? rest : rest[..end];
            if (WriteCall.IsMatch(rest))
            {
                yield return new StoreAccess(m.Groups["t"].Value, DataAccess.Write, m.Value);
            }
            else if (ReadCall.IsMatch(rest))
            {
                yield return new StoreAccess(m.Groups["t"].Value, DataAccess.Read, m.Value);
            }
        }
    }

    [GeneratedRegex("\\bcreate\\s+(?:virtual\\s+)?table\\s+(?:if\\s+not\\s+exists\\s+)?" + Qualified, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CreateTable { get; }

    [GeneratedRegex("\\b(?:from|join)\\s+" + Qualified, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FromOrJoin { get; }

    [GeneratedRegex("\\bselect\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SelectWord { get; }

    [GeneratedRegex("\\bdelete\\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DeleteBefore { get; }

    [GeneratedRegex("\\binsert\\s+(?:or\\s+\\w+\\s+)?into\\s+" + Qualified, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex InsertInto { get; }

    [GeneratedRegex("\\bupdate\\s+" + Qualified + "\\s+set\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UpdateSet { get; }

    [GeneratedRegex("\\bdelete\\s+from\\s+" + Qualified, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DeleteFrom { get; }

    [GeneratedRegex("\\.(?:from|table|into)\\(\\s*['\"`](?:[\\w$]+\\.)*(?<t>[\\w$]+)['\"`]\\s*\\)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BuilderTable { get; }

    [GeneratedRegex("\\.(?:insert|update|delete|del|upsert)\\(", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WriteCall { get; }

    [GeneratedRegex("\\.(?:select|where|first|pluck|count)\\(", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ReadCall { get; }
}
