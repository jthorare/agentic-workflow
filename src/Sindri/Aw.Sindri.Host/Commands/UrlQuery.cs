using System.Text;

namespace Aw.Sindri.Host.Commands;

/// <summary>The query string a verb sends: the pairs it has a value for, each value percent-encoded.</summary>
internal static class UrlQuery
{
    /// <summary>'?a=1&amp;b=2' for the pairs whose value isn't null, in order; empty when none is.</summary>
    /// <param name="pairs">The names and values; a null value leaves its pair out.</param>
    /// <returns>The query string with its leading '?', or an empty string.</returns>
    internal static string Of(IEnumerable<(string Key, string? Value)> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        var query = new StringBuilder();
        foreach (var (key, value) in pairs)
        {
            if (value is not null)
            {
                _ = query.Append(query.Length == 0 ? '?' : '&').Append(key).Append('=').Append(Uri.EscapeDataString(value));
            }
        }

        return query.ToString();
    }
}
