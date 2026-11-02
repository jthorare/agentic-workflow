using System.Text;

namespace Aw.Context.Core.Graph;

/// <summary>The names offered for a page and its actions (pages spec §3.2).</summary>
public static class PageNames
{
    private static readonly string[] _dropped = ["Page", "Screen", "Dialog", "Modal"];

    /// <summary>The name offered for a page.</summary>
    /// <param name="node">The page's surface node.</param>
    /// <returns>A route's path segments as words ('GET:/schedule/week' becomes 'Schedule week', parameters dropped); a component name split at capitals with Page, Screen, Dialog and Modal dropped ('ScheduleWeekPage' becomes 'Schedule week'); else the node's label.</returns>
    public static string Offer(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var label = node.Label;
        var slash = label.IndexOf('/', StringComparison.Ordinal);
        if (slash >= 0)
        {
            var path = label[slash..];
            var words = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(s => !s.StartsWith(':') && !s.StartsWith('{') && !s.StartsWith('[')).SelectMany(Words).ToList();
            return words.Count == 0 ? label : Sentence(words);
        }

        if (label.Length > 0 && !label.Contains(' ', StringComparison.Ordinal) && char.IsUpper(label[0]))
        {
            var words = Words(label).Where(w => !_dropped.Contains(w, StringComparer.Ordinal)).ToList();
            return words.Count == 0 ? label : Sentence(words);
        }

        return label;
    }

    private static string Sentence(List<string> words)
    {
        var text = string.Join(' ', words.Select(w => w.ToLowerInvariant()));
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    // Splits at capitals and at '-' or '_' ('ScheduleWeek' -> Schedule, Week).
    private static IEnumerable<string> Words(string text)
    {
        var current = new StringBuilder();
        foreach (var c in text)
        {
            if (c is '-' or '_')
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    _ = current.Clear();
                }
            }
            else
            {
                if (char.IsUpper(c) && current.Length > 0 && !char.IsUpper(current[^1]))
                {
                    yield return current.ToString();
                    _ = current.Clear();
                }

                _ = current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }
}
