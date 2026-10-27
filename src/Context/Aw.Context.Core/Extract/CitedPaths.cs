namespace Aw.Context.Core.Extract;

/// <summary>The repo paths a doc's text cites: backticked paths and 'file.ext:12' references.</summary>
internal static class CitedPaths
{
    /// <summary>Finds the cited paths.</summary>
    /// <param name="text">The doc's text.</param>
    /// <returns>The distinct cited paths, in order of first mention.</returns>
    internal static IReadOnlyList<string> Find(string text) => throw new NotSupportedException();
}
