using System.Globalization;
using Aw.Kernel;

namespace Aw.Context.Core.Engine;

public static partial class QueryRules
{
    /// <summary>The most keys a subgraph GET takes (scoping spec C14).</summary>
    public const int MaxSubgraphGet = 20;

    /// <summary>Whether a subgraph question fits a URL: at most <see cref="MaxSubgraphGet"/> distinct keys, each at most <see cref="MaxEncodedText"/> characters once percent-encoded.</summary>
    /// <param name="keys">The node keys as given.</param>
    /// <returns>Null when it fits, else the 006.</returns>
    public static Error? CheckSubgraphGet(IReadOnlyList<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var distinct = keys.Distinct(StringComparer.Ordinal).ToList();
        return distinct.Count > MaxSubgraphGet
            ? ContextErrors.QueryInvalid.With(string.Create(CultureInfo.InvariantCulture, $"a subgraph GET takes at most {MaxSubgraphGet} keys, not {distinct.Count} (fix: send the keys in a QUERY body)"))
            : distinct.Exists(k => !FitsQueryUrl(k)) ? ContextErrors.QueryInvalid.With(string.Create(CultureInfo.InvariantCulture, $"a subgraph key is over {MaxEncodedText} characters once percent-encoded for the query (fix: send the keys in a QUERY body)"))
            : null;
    }
}
