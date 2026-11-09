namespace Aw.Sindri.Ui.Api.Memory;

/// <summary>The paths of the memory calls. The name is escaped as one segment.</summary>
internal static class MemoryRoutes
{
    /// <summary>The list and the create.</summary>
    internal const string Root = "/sindri/memories";

    internal static string Facts(string name) => $"{Root}/{Uri.EscapeDataString(name)}/facts";

    internal static string Search(MemorySearch search)
    {
        var path = $"{Root}/{Uri.EscapeDataString(search.Name)}/search?query={Uri.EscapeDataString(search.Query)}";
        if (!string.IsNullOrEmpty(search.Level))
        {
            path += $"&level={Uri.EscapeDataString(search.Level)}";
        }

        return search.Limit is { } n ? path + "&limit=" + n.ToString(System.Globalization.CultureInfo.InvariantCulture) : path;
    }

    internal static string Forget(MemoryForget forget)
    {
        var (name, factId, tag, expired) = forget;
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(factId))
        {
            parts.Add($"fact={Uri.EscapeDataString(factId)}");
        }

        if (!string.IsNullOrEmpty(tag))
        {
            parts.Add($"tag={Uri.EscapeDataString(tag)}");
        }

        if (expired)
        {
            parts.Add("expired=true");
        }

        return parts.Count == 0 ? Facts(name) : $"{Facts(name)}?{string.Join('&', parts)}";
    }
}
