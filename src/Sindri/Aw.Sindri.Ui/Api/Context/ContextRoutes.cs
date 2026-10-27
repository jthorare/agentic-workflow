using System.Globalization;
using System.Text;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The paths of the context routes (context spec §8). A key, a path or an anchor is only ever a query value, escaped, never a path segment.</summary>
internal static class ContextRoutes
{
    private const string Root = "/sindri/context";

    /// <summary>POST and GET /sindri/context/map/review.</summary>
    internal const string Review = Root + "/map/review";

    /// <summary>GET /sindri/context/map/publish.</summary>
    internal const string MapPublish = Root + "/map/publish";

    /// <summary>GET /sindri/context/sources.</summary>
    internal const string Sources = Root + "/sources";

    /// <summary>QUERY /sindri/context/lenses: the anchors travel as the JSON body, so the list has no length limit.</summary>
    internal const string Lenses = Root + "/lenses";

    internal static string WholeMap(WholeMapQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var parts = new List<string>();
        Add(parts, "detail", query.Detail);
        Add(parts, "area", query.Area);
        Add(parts, "repo", query.Repo);
        Add(parts, "concept", query.Concept);
        parts.AddRange(query.Locate.Select(k => "locate=" + Uri.EscapeDataString(k)));
        if (query.Draft)
        {
            parts.Add("draft=true");
        }

        Add(parts, "mapVersion", query.MapVersion);
        return With(Root + "/map/whole", parts);
    }

    internal static string Concepts(ConceptsQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var parts = new List<string>();
        Add(parts, "repo", query.Repo);
        Add(parts, "cursor", query.Cursor);
        parts.AddRange(Limit(query.Limit));
        Add(parts, "q", query.Q);
        return With(Root + "/concepts", parts);
    }

    internal static string ConceptGlossary(ConceptGlossaryQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var parts = new List<string>();
        Add(parts, "repo", query.Repo);
        Add(parts, "mapVersion", query.MapVersion);
        return With(Root + "/concepts/glossary", parts);
    }

    /// <summary>QUERY /sindri/context/code: the anchors travel as the JSON body.</summary>
    internal const string Code = Root + "/code";

    internal static string FileMap(FileMapQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var parts = new List<string> { "repo=" + Uri.EscapeDataString(query.Repo) };
        Add(parts, "path", query.Path);
        if (query.UnmappedOnly)
        {
            parts.Add("unmapped=true");
        }

        if (query.Draft)
        {
            parts.Add("draft=true");
        }

        Add(parts, "cursor", query.Cursor);
        parts.AddRange(Limit(query.Limit));
        return With(Root + "/filemap", parts);
    }

    internal static string ReviewQueue(int? limit) => With(Review, Limit(limit));

    internal static string ReviewHistory(int? limit) => With(Review + "/history", Limit(limit));

    internal static string MapStatus(string? anchor)
    {
        var parts = new List<string>();
        Add(parts, "anchor", anchor);
        return With(Root + "/map", parts);
    }

    internal static string Query(string text) => With(Root + "/query", ["q=" + Uri.EscapeDataString(text)]);

    internal static string Node(string key) => With(Root + "/node", ["key=" + Uri.EscapeDataString(key)]);

    private static List<string> Limit(int? limit) => limit is { } n ? [string.Create(CultureInfo.InvariantCulture, $"limit={n}")] : [];

    private static void Add(List<string> parts, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            parts.Add($"{name}={Uri.EscapeDataString(value)}");
        }
    }

    private static string With(string path, List<string> parts) => parts.Count == 0 ? path : new StringBuilder(path).Append('?').AppendJoin('&', parts).ToString();
}
