using System.Text.RegularExpressions;
using Aw.Context.Core.Extract.Imports;

namespace Aw.Context.Core.Extract.DataStores;

/// <summary>
/// Drizzle (TypeScript): a table is 'pgTable|sqliteTable|mysqlTable('name', …)' in a schema file, bound to an exported identifier; code reads one with
/// '.from(t)', a join ('.leftJoin(t, …)') or 'db.query.t.findMany|findFirst', and writes it with '.insert(t)', '.update(t)' or '.delete(t)'. What 't' stands for comes
/// from the file's imports (the scope), so a renamed import, a namespace import and a barrel re-export all reach the table, and a name that is not a declared table is no use.
/// Tables declared inside a function or from a computed name are not seen.
/// </summary>
internal sealed partial class DrizzleAdapter : IBindingDataStoreAdapter
{
    /// <inheritdoc />
    public string Name => "drizzle";

    /// <inheritdoc />
    public IEnumerable<string> Declared(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Safe<string>(() => [.. Table.Matches(Comments.Strip(text)).Select(m => m.Groups["n"].Value)]);
    }

    /// <inheritdoc />
    public IEnumerable<StoreBinding> DeclaredBindings(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Safe<StoreBinding>(() => [.. Bound.Matches(Comments.Strip(text)).Select(m => new StoreBinding(m.Groups["i"].Value, m.Groups["n"].Value))]);
    }

    /// <inheritdoc />
    public IEnumerable<StoreAccess> Accesses(string text, IReadOnlySet<string> stores)
    {
        ArgumentNullException.ThrowIfNull(stores);
        return Accesses(text, new SameNameScope(stores));   // no imports to follow: a name that is a store's own name stands for it
    }

    /// <inheritdoc />
    public IEnumerable<StoreAccess> Accesses(string text, IStoreScope scope)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(scope);
        return Safe<StoreAccess>(() =>
        {
            var clean = Comments.Strip(text);
            return [.. Uses(clean, Reads, DataAccess.Read, scope).Concat(Uses(clean, Writes, DataAccess.Write, scope)).Concat(Keyed(clean, scope))
                .DistinctBy(a => (a.Store.ToLowerInvariant(), a.Access))];
        });
    }

    private static IEnumerable<StoreAccess> Uses(string text, Regex pattern, DataAccess access, IStoreScope scope)
    {
        foreach (Match m in pattern.Matches(text))
        {
            var target = m.Groups["t"].Value;
            var dot = target.IndexOf('.', StringComparison.Ordinal);
            var store = dot < 0 ? scope.Store(target) : scope.Member(target[..dot], target[(dot + 1)..]);
            if (store is not null)
            {
                yield return new StoreAccess(store, access, m.Value);
            }
        }
    }

    private static IEnumerable<StoreAccess> Keyed(string text, IStoreScope scope)
    {
        foreach (Match m in Query.Matches(text))
        {
            if (scope.Key(m.Groups["k"].Value) is { } store)
            {
                yield return new StoreAccess(store, DataAccess.Read, m.Value);
            }
        }
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

    private sealed class SameNameScope(IReadOnlySet<string> stores) : IStoreScope
    {
        public string? Store(string local) => stores.Contains(local.ToLowerInvariant()) ? local : null;

        public string? Member(string ns, string member) => null;

        public string? Key(string key) => Store(key);
    }

    [GeneratedRegex("\\b(?:pg|sqlite|mysql)Table\\s*\\(\\s*(?<q>[\"'`])(?<n>[^\"'`\\r\\n]+)\\k<q>", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Table { get; }

    [GeneratedRegex("\\bexport\\s+const\\s+(?<i>[A-Za-z_$][\\w$]*)\\s*=\\s*(?:pg|sqlite|mysql)Table\\s*\\(\\s*(?<q>[\"'`])(?<n>[^\"'`\\r\\n]+)\\k<q>", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Bound { get; }

    [GeneratedRegex("\\.(?:from|leftJoin|rightJoin|innerJoin|fullJoin|crossJoin)\\(\\s*(?<t>[A-Za-z_$][\\w$]*(?:\\.[A-Za-z_$][\\w$]*)?)\\s*[,)]", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Reads { get; }

    [GeneratedRegex("\\.(?:insert|update|delete)\\(\\s*(?<t>[A-Za-z_$][\\w$]*(?:\\.[A-Za-z_$][\\w$]*)?)\\s*\\)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Writes { get; }

    [GeneratedRegex("\\.query\\.(?<k>[A-Za-z_$][\\w$]*)\\.find(?:Many|First)\\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Query { get; }
}
