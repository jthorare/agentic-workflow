using System.Globalization;
using Aw.Context.Core.Lenses;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The words the CLI puts after a symbols state: the reason in parentheses, or "from abcdef0, not 1234567" when the code index and the map are at different commits (S-9a).</summary>
internal static class SymbolWords
{
    private const int ShortLength = 7;

    /// <summary>The suffix for a symbols object: empty when there is nothing to say.</summary>
    /// <param name="reason">The recorded-limit word, or null.</param>
    /// <param name="commit">The commit the symbols were read at, or null.</param>
    /// <param name="asOfCommit">The map's commit when it differs, or null.</param>
    /// <returns>Text starting with a space, or empty.</returns>
    internal static string Why(string? reason, string? commit, string? asOfCommit)
    {
        if (commit is not null && asOfCommit is not null)
        {
            return string.Equals(reason, "catching-up", StringComparison.Ordinal)
                ? $" (catching up: from {Short(commit)}, map at {Short(asOfCommit)})"
                : $" (from {Short(commit)}, not {Short(asOfCommit)})";
        }

        return reason is null ? string.Empty : $" ({reason})";
    }

    /// <summary>The line under a catching-up source: both commits and every pending file, or empty for any other source.</summary>
    /// <param name="catchingUp">The source's catching-up note, or null.</param>
    /// <returns>An indented line ending in a newline, or empty.</returns>
    internal static string Pending(SearchedCatchingUp? catchingUp) =>
        catchingUp is null
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"    index at {Short(catchingUp.IndexCommit)}, map at {Short(catchingUp.MapCommit)}; pending ({catchingUp.Pending.Count}): {(catchingUp.Pending.Count == 0 ? "none" : string.Join(", ", catchingUp.Pending))}\n");

    private static string Short(string commit) => commit.Length <= ShortLength ? commit : commit[..ShortLength];
}
