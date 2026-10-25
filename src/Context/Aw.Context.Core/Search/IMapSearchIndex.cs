using Aw.Kernel;

namespace Aw.Context.Core.Search;

/// <summary>A built full-text index over one map's documents. Safe to ask from many threads.</summary>
public interface IMapSearchIndex
{
    /// <summary>The documents that hold every word, ranked best first (title over path over text); the last word also matches as a prefix.</summary>
    /// <param name="terms">The words, from <see cref="MapSearchRules.Terms"/>.</param>
    /// <param name="area">One area's key to stay within, or null.</param>
    /// <param name="limit">How many matches to return.</param>
    /// <returns>The page, or SND-CONTEXT-004 when the index can't be read.</returns>
    public Result<MapSearchPage> Search(IReadOnlyList<string> terms, string? area, int limit);
}
