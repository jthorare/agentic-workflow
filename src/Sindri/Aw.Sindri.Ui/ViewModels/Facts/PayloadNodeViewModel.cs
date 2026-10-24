using System.Globalization;
using System.Text.Json;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>One node of a payload's tree: a key, then its scalar value or the size of what it holds, and its children. Built from the body with a depth and a width limit, so a huge body can't stall the panel.</summary>
internal sealed class PayloadNodeViewModel
{
    private const int MaxDepth = 8;
    private const int MaxChildren = 1000;
    private const int MaxValue = 2000;

    private PayloadNodeViewModel(string key, string text, IReadOnlyList<PayloadNodeViewModel> children)
    {
        (Key, Text, Children) = (key, text, children);
    }

    /// <summary>The key, or the index of an array item.</summary>
    public string Key { get; }

    /// <summary>The node's words: "key: value" for a scalar, "key" and its size for a container.</summary>
    public string Text { get; }

    /// <summary>The children of an object or an array.</summary>
    public IReadOnlyList<PayloadNodeViewModel> Children { get; }

    /// <summary>The nodes of a body: an object's properties, an array's items, and none for a scalar.</summary>
    /// <param name="body">The body.</param>
    /// <returns>The top-level nodes.</returns>
    internal static IReadOnlyList<PayloadNodeViewModel> Roots(JsonElement body) => Members(body, depth: 0);

    /// <summary>One line for a body that is not an object: a scalar's value, or the size of an array.</summary>
    /// <param name="body">The body.</param>
    /// <returns>The words.</returns>
    internal static string Line(JsonElement body) => Describe(string.Empty, body).Trim();

    private static List<PayloadNodeViewModel> Members(JsonElement element, int depth)
    {
        var nodes = new List<PayloadNodeViewModel>();
        if (depth >= MaxDepth)
        {
            return nodes;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            nodes.AddRange(element.EnumerateObject().Take(MaxChildren).Select(p => Node(p.Name, p.Value, depth + 1)));
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            nodes.AddRange(element.EnumerateArray().Take(MaxChildren).Select((item, i) => Node(i.ToString(CultureInfo.InvariantCulture), item, depth + 1)));
        }

        return nodes;
    }

    private static PayloadNodeViewModel Node(string key, JsonElement value, int depth)
    {
        var scrubbed = FactText.Scrub(key);
        return new PayloadNodeViewModel(scrubbed, Describe(scrubbed, value), Members(value, depth));
    }

    private static string Describe(string key, JsonElement value)
    {
#pragma warning disable IDE0072 // every other kind reads as its raw text
        return value.ValueKind switch
        {
            JsonValueKind.Object => string.Create(CultureInfo.InvariantCulture, $"{key} {{{value.EnumerateObject().Count()} keys}}"),
            JsonValueKind.Array => string.Create(CultureInfo.InvariantCulture, $"{key} [{value.GetArrayLength()} items]"),
            JsonValueKind.String => Scalar(key, FactText.Scrub(value.GetString() ?? string.Empty)),
            JsonValueKind.Null => Scalar(key, "null"),
            _ => Scalar(key, value.GetRawText()),
        };
#pragma warning restore IDE0072
    }

    private static string Scalar(string key, string value)
    {
        var shown = value.Length > MaxValue ? value[..MaxValue] + "…" : value;
        return key.Length > 0 ? $"{key}: {shown}" : shown;
    }
}
