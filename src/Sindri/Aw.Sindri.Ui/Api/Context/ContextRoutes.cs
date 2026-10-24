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

    internal static string Lenses(LensesQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var parts = new List<string>();
        parts.AddRange(query.Anchors.Select(a => "anchor=" + Uri.EscapeDataString(a)));
        if (query.Depth is { } depth)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"depth={depth}"));
        }

        if (query.Draft)
        {
            parts.Add("draft=true");
        }

        Add(parts, "mapVersion", query.MapVersion);
        return With(Root + "/lenses", parts);
    }

    internal static string WholeMap(WholeMapQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var parts = new List<string>();
        Add(parts, "detail", query.Detail);
        Add(parts, "area", query.Area);
        Add(parts, "repo", query.Repo);
        parts.AddRange(query.Locate.Select(k => "locate=" + Uri.EscapeDataString(k)));
        if (query.Draft)
        {
            parts.Add("draft=true");
        }

        Add(parts, "mapVersion", query.MapVersion);
        return With(Root + "/map/whole", parts);
    }

    internal static string Code(CodeQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var parts = new List<string>();
        parts.AddRange(query.Anchors.Select(a => "anchor=" + Uri.EscapeDataString(a)));
        if (query.Draft)
        {
            parts.Add("draft=true");
        }

        Add(parts, "mapVersion", query.MapVersion);
        return With(Root + "/code", parts);
    }

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
