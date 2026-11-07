using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core.Trust;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// Fences the untrusted free text of a sindri_* result (API spec §8, "the same trust field and fencing as context_* results"):
/// each planned string is wrapped by <see cref="UntrustedFence"/> and its object gains a trust member set to "untrusted". Nothing
/// else changes, so structuredContent equals the HTTP body apart from those values and members.
/// </summary>
internal static class McpFencing
{
    // why: the host's AwJson writes with this encoder, so unplanned strings come out as they went in
    private static readonly JsonSerializerOptions _write = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The member that marks an object as holding fenced text.</summary>
    internal const string TrustMember = "trust";

    /// <summary>The value of <see cref="TrustMember"/>.</summary>
    internal const string Untrusted = "untrusted";

    /// <summary>The body with every field of the plan fenced.</summary>
    /// <param name="body">The route's JSON body.</param>
    /// <param name="plan">What to fence.</param>
    /// <returns>The fenced body.</returns>
    internal static JsonElement Apply(JsonElement body, FencePlan plan)
    {
        var node = JsonNode.Parse(body.GetRawText());
        foreach (var field in plan.Fields)
        {
            Fence(node, field, field.Path.Split('.'), 0, plan.Source);
        }

        using var document = JsonDocument.Parse(node is null ? "null" : node.ToJsonString(_write));
        return document.RootElement.Clone();
    }

    private static void Fence(JsonNode? node, FenceField field, string[] path, int depth, string source)
    {
        var segment = path[depth];
        var many = segment.EndsWith("[]", StringComparison.Ordinal);
        var name = many ? segment[..^2] : segment;
        if (node is not JsonObject owner || owner[name] is not { } child)
        {
            return;
        }

        var last = depth == path.Length - 1;
        if (last)
        {
            FenceLeaf(owner, segment, child, field, source);
        }
        else if (many)
        {
            foreach (var element in child is JsonArray array ? array.ToList() : [])
            {
                Fence(element, field, path, depth + 1, source);
            }
        }
        else
        {
            Fence(child, field, path, depth + 1, source);
        }
    }

    private static void FenceLeaf(JsonObject owner, string segment, JsonNode child, FenceField field, string source)
    {
        var many = segment.EndsWith("[]", StringComparison.Ordinal);
        var name = many ? segment[..^2] : segment;
        var author = AuthorOf(owner, field.AuthorField);
        if (many)
        {
            MarkIf(owner, child is JsonArray strings && FenceStrings(strings, source, author));
        }
        else if (field.EveryString)
        {
            var fenced = FenceTree(child, source, author, out var any);
            if (!ReferenceEquals(fenced, child))
            {
                owner[name] = fenced;
            }

            MarkIf(owner, any);
        }
        else if (child is JsonValue value && value.TryGetValue<string>(out var text))
        {
            owner[name] = UntrustedFence.Wrap(source, author, text);
            if (field.Mark)
            {
                owner[TrustMember] = Untrusted;
            }
        }
    }

    // why: returns the node to store back, because a bare string is replaced rather than changed in place
    private static JsonNode? FenceTree(JsonNode? node, string source, string? author, out bool any)
    {
        any = false;
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var text):
                any = true;
                return UntrustedFence.Wrap(source, author, text);
            case JsonArray array:
                any = FenceStrings(array, source, author);
                return array;
            case JsonObject members:
                foreach (var (key, member) in members.ToList())
                {
                    var fenced = FenceTree(member, source, author, out var inside);
                    if (!ReferenceEquals(fenced, member))
                    {
                        members[key] = fenced;
                    }

                    if (inside)
                    {
                        any = true;
                    }
                }

                MarkIf(members, any);
                return members;
            default:
                return node;
        }
    }

    // a deep array fences its nested strings and objects too; the result says whether anything was fenced
    private static bool FenceStrings(JsonArray array, string source, string? author)
    {
        var any = false;
        for (var i = 0; i < array.Count; i++)
        {
            var fenced = FenceTree(array[i], source, author, out var inside);
            if (!ReferenceEquals(fenced, array[i]))
            {
                array[i] = fenced;
            }

            any |= inside;
        }

        return any;
    }

    private static void MarkIf(JsonObject owner, bool fenced)
    {
        if (fenced)
        {
            owner[TrustMember] = Untrusted;
        }
    }

    private static string? AuthorOf(JsonObject owner, string? authorField) =>
        authorField is not null && owner[authorField] is JsonValue who && who.TryGetValue<string>(out var by) ? by : null;
}
