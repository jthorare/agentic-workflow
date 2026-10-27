namespace Aw.Context.Features.SearchMap;

/// <summary>Picks which field matched a question and the stretch of it to show.</summary>
internal static class MapSearchSnippets
{
    private const int Width = 160;
    private const int Lead = 60;

    /// <summary>The first of title, path and text that holds one of <paramref name="terms"/>, and a window of it around the first hit (the title or path whole when short). No field holds a word (the index matched a folded form): the title.</summary>
    internal static (string Matched, string Snippet) Of(Core.Search.MapSearchDoc doc, IReadOnlyList<string> terms)
    {
        foreach (var (name, raw) in new[] { ("title", doc.Title), ("path", doc.Path), ("text", doc.Text) })
        {
            var field = OneLine(raw);
            var at = terms.Select(t => field.IndexOf(t, StringComparison.OrdinalIgnoreCase)).Where(static i => i >= 0).DefaultIfEmpty(-1).Min();
            if (at >= 0)
            {
                return (name, Window(field, at));
            }
        }

        return ("title", Window(OneLine(doc.Title), 0));
    }

    private static string OneLine(string field) => string.Join(' ', field.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Window(string line, int at)
    {
        if (line.Length <= Width)
        {
            return line;
        }

        var start = Math.Clamp(at - Lead, 0, line.Length - Width);
        return string.Concat(start > 0 ? "…" : string.Empty, line.AsSpan(start, Width), start + Width < line.Length ? "…" : string.Empty);
    }
}
