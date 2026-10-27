using System.Globalization;
using Aw.Kernel;

namespace Aw.Context.Core.Concepts;

/// <summary>The paging of the concepts reads: an offset into a list in a fixed order, bound to the map version it was read at so a new version never resumes the old list.</summary>
internal static class ConceptPages
{
    /// <summary>The page size when the question names none.</summary>
    internal const int DefaultLimit = 100;

    /// <summary>The slice a page asks for.</summary>
    /// <param name="mapVersion">The map version read ('v13').</param>
    /// <param name="page">The question's page.</param>
    /// <returns>The offset and the size, or SND-CONTEXT-006 for a limit below 1 or a cursor that isn't this map version's.</returns>
    internal static Result<(int Offset, int Size)> Slice(string mapVersion, ConceptPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (page.Limit is < 1)
        {
            return ContextErrors.QueryInvalid.With("'limit' is at least 1 (fix: ask for one or more, or leave it out)");
        }

        var size = page.Limit ?? DefaultLimit;
        if (page.Cursor is null)
        {
            return (0, size);
        }

        var prefix = mapVersion + ":";
        return page.Cursor.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(page.Cursor.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var offset)
            ? (offset, size)
            : ContextErrors.QueryInvalid.With("'cursor' isn't from this map version (fix: start again without a cursor)");
    }

    /// <summary>The cursor after a page, or null when the page reached the end.</summary>
    /// <param name="mapVersion">The map version read.</param>
    /// <param name="end">The offset after the page's last entry.</param>
    /// <param name="total">How many entries the list holds.</param>
    /// <returns>The cursor, or null.</returns>
    internal static string? Next(string mapVersion, int end, int total) =>
        end < total ? mapVersion + ":" + end.ToString(CultureInfo.InvariantCulture) : null;
}
