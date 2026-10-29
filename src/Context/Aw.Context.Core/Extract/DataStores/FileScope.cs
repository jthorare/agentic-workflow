using Aw.Context.Core.Extract.Imports;

namespace Aw.Context.Core.Extract.DataStores;

/// <summary>What the names of one code file stand for: its imports followed to the file that declares them, and that file's identifier to store bindings.</summary>
/// <param name="file">The code file's repo-relative path.</param>
/// <param name="resolver">Follows its imports.</param>
/// <param name="bindings">The identifiers the schema files declare, by file, with the store each stands for (lower case).</param>
internal sealed class FileScope(string file, TsSymbolResolver resolver, IReadOnlyDictionary<(string File, string Identifier), string> bindings) : IStoreScope
{
    /// <inheritdoc />
    public string? Store(string local) => Of(resolver.Resolve(file, local));

    /// <inheritdoc />
    public string? Member(string ns, string member) => Of(resolver.Member(file, ns, member));

    /// <inheritdoc />
    public string? Key(string key)
    {
        var named = bindings.Where(b => string.Equals(b.Key.Identifier, key, StringComparison.Ordinal)).Select(b => b.Value).Distinct(StringComparer.Ordinal).Take(2).ToList();
        return named.Count == 1 ? named[0] : null;
    }

    private string? Of(ResolvedSymbol? symbol) => symbol is not null && bindings.TryGetValue((symbol.File, symbol.Name), out var store) ? store : null;
}
