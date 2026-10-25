using System.Collections.Frozen;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// Pages a tool answer: every top-level list keeps its own window of items (the same offset and limit, applied to each list
/// by its own index, so a page is stable whatever the other lists hold), and page.more says another page follows in some
/// list. An item is never cut inside: its evidence, sources and other arrays stay whole. Counts and other objects stay the
/// full answer. A degraded source is a warning, not a result, so that list is never cut; the citations are cut to those the page's items name. The section and group buckets stay whole, because they are the answer's shape and their lists are what pages.
/// </summary>
internal static class McpPages
{
    // why: the host's AwJson writes with this encoder, so a page is the same text with lists cut
    private static readonly JsonSerializerOptions _write = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // why: these arrays are the buckets (one section or queue group each), not a list a caller pages through
    private static readonly FrozenSet<string> _buckets = FrozenSet.Create(StringComparer.Ordinal, "sections", "groups");

    // why: a warning is not a result list. A source that failed must never be hidden by the window, so these stay whole.
    private static readonly FrozenSet<string> _warnings = FrozenSet.Create(StringComparer.Ordinal, "degraded");

    // why: the citations are looked up by marker; a page keeps only the ones its own items and conflicts name, so a page shrinks with its window
    private const string Cites = "cites";

    /// <summary>The answer with one page of each top-level list, and a page object.</summary>
    /// <param name="answer">The whole answer.</param>
    /// <param name="window">Which page.</param>
    /// <returns>The page. A non-object answer is returned unchanged.</returns>
    internal static JsonElement Apply(JsonElement answer, McpWindow window)
    {
        if (JsonNode.Parse(answer.GetRawText()) is not JsonObject root)
        {
            return answer;
        }

        var more = false;
        var lists = new JsonObject();
        // note: an answer that already has a top-level "page" loses it here; none does today
        Slice(root, window, "", lists, ref more);
        KeepPageCites(root);
        root["page"] = new JsonObject
        {
            ["offset"] = window.Offset,
            ["limit"] = window.Limit,
            ["more"] = more,
            ["lists"] = lists,
        };
        using var document = JsonDocument.Parse(root.ToJsonString(_write));
        return document.RootElement.Clone();
    }

    // Only the object's own arrays are lists; a bucket's objects are sliced one level down, and nothing is sliced inside an item.
    private static void Slice(JsonObject obj, McpWindow window, string path, JsonObject lists, ref bool more)
    {
        foreach (var name in obj.Select(property => property.Key).ToArray())
        {
            if (path.Length == 0 && name == "bands" && obj[name] is JsonObject bands)
            {
                SliceBands(bands, window, lists, ref more);
                continue;
            }

            if (obj[name] is not JsonArray array || (path.Length == 0 && _warnings.Contains(name)))
            {
                continue;
            }

            if (path.Length == 0 && _buckets.Contains(name))
            {
                SliceBucket(name, array, window, lists, ref more);
                continue;
            }

            obj[name] = Cut(array, window, path + name, lists, ref more);
        }
    }

    // Drops the citations no kept item or conflict names (a marker is any string value in the page outside 'cites' and 'page').
    private static void KeepPageCites(JsonObject root)
    {
        if (root[Cites] is not JsonObject cites)
        {
            return;
        }

        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.Where(p => p.Key != Cites))
        {
            Collect(property.Value, named);
        }

        foreach (var marker in cites.Select(c => c.Key).Where(k => !named.Contains(k)).ToArray())
        {
            _ = cites.Remove(marker);
        }
    }

    private static void Collect(JsonNode? node, HashSet<string> strings)
    {
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var text):
                _ = strings.Add(text);
                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    Collect(child, strings);
                }

                break;
            case JsonObject obj:
                foreach (var property in obj)
                {
                    Collect(property.Value, strings);
                }

                break;
            default:
                break;
        }
    }

    // A bucket array (the answer's sections or groups): each bucket object is sliced one level down, keyed by its own name.
    private static void SliceBucket(string name, JsonArray buckets, McpWindow window, JsonObject lists, ref bool more)
    {
        for (var index = 0; index < buckets.Count; index++)
        {
            if (buckets[index] is JsonObject child)
            {
                // why: the bucket's name keys the list, so 'sections.graph.items' means the same list on every page and in every answer
                var label = BucketName(child) ?? index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                Slice(child, window, $"{name}.{label}.", lists, ref more);
            }
        }
    }

    private static readonly string[] _bucketNameKeys = ["name", "group"];

    // A bucket is named by its 'name' (a section) or its 'group' (a queue group).
    private static string? BucketName(JsonObject bucket) =>
        _bucketNameKeys.Select(key => bucket[key] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null).FirstOrDefault(text => text is not null);

    // why: the lenses answer groups its lists in three band objects; each band's lists page, nothing inside an item does
    private static void SliceBands(JsonObject bands, McpWindow window, JsonObject lists, ref bool more)
    {
        foreach (var band in bands.Select(property => property.Key).ToArray())
        {
            if (bands[band] is JsonObject child)
            {
                Slice(child, window, $"bands.{band}.", lists, ref more);
            }
        }
    }

    private static JsonArray Cut(JsonArray array, McpWindow window, string path, JsonObject lists, ref bool more)
    {
        var listMore = array.Count > (long)window.Offset + window.Limit;
        more |= listMore;
        lists[path] = new JsonObject { ["total"] = array.Count, ["more"] = listMore };
        var cut = new JsonArray();
        if (window.Offset >= array.Count)
        {
            return cut;
        }

        var count = Math.Min(window.Limit, array.Count - window.Offset);
        for (var index = 0; index < count; index++)
        {
            cut.Add(array[window.Offset + index]?.DeepClone());
        }

        return cut;
    }
}
