using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.GetInstanceFact;

/// <summary>
/// The scrubbed body behind one fact row (API spec §3.8): the instance's view names the declared facts and their stored digests, the
/// fact store reads the content, the scrubber removes secrets, and the body is capped at <see cref="CapBytes"/>. A text fact is a
/// string; any other type is its stored JSON, which stays an object when cut (it loses its tail entries). A key the recipe doesn't
/// declare, or one not produced yet, answers SND-ITEM-404, as does an unknown instance.
/// </summary>
/// <param name="directory">Resolves the instance id to its address.</param>
/// <param name="runtime">The instances on Temporal.</param>
/// <param name="contents">The stored fact content.</param>
/// <param name="scrubber">Removes secrets from the text.</param>
/// <param name="callers">The caller; a read needs one.</param>
internal sealed class GetInstanceFactService(IInstanceDirectory directory, IInstanceReader runtime, IFactContents contents, ITextScrubber scrubber, ICallerContextAccessor callers)
    : IService<GetInstanceFactRequest, FactView>
{
    /// <summary>The largest body served: 256 KiB of UTF-8.</summary>
    internal const int CapBytes = 256 * 1024;

    public async ValueTask<Result<FactView>> Handle(GetInstanceFactRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("a fact read needs a caller");
        }

        var address = await directory.Resolve(request.Instance.Value, cancellationToken).ConfigureAwait(false);
        if (address.IsError)
        {
            return InstanceScope.Unknown(address.Error, request.Instance.Value);
        }

        var view = await runtime.Get(address.Value, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return InstanceScope.Unknown(view.Error, request.Instance.Value);
        }

        var row = view.Value.Facts.FirstOrDefault(f => string.Equals(f.Key, request.Key, StringComparison.Ordinal));
        if (row is null || view.Value.FactShas?.GetValueOrDefault(request.Key) is not { } sha)
        {
            return SindriErrors.ItemNotFound.With(row is null ? $"no fact '{request.Key}' on the instance" : $"the fact '{request.Key}' is not produced yet");
        }

        var bytes = await contents.Read(new FactValue("fact:sha256:" + sha, sha, HeadSha: null), cancellationToken).ConfigureAwait(false);
        return bytes.IsError ? bytes.Error : Body(row, scrubber.Scrub(Encoding.UTF8.GetString(bytes.Value.Span)));
    }

    private static FactView Body(FactRow row, string text)
    {
        if (row.Type != FactType.Text && Parse(text) is { } node)
        {
            var (cut, truncated) = Cap(node);
            return new FactView(row.Key, row.Type, Element(cut), truncated);
        }

        var (kept, clipped) = CutText(text);
        return new FactView(row.Key, row.Type, Element(JsonValue.Create(kept)), clipped);
    }

    private static JsonElement Element(JsonNode node)
    {
        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }

    /// <summary>The stored text as JSON when it is an object or an array; null otherwise (it is then served as a string).</summary>
    private static JsonNode? Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text) is { } node && node is JsonObject or JsonArray ? node : null;
        }
        catch (JsonException)
        {
            return null;   // boundary: stored content that isn't JSON is served as text
        }
    }

    /// <summary>
    /// The node cut to the cap by dropping its tail entries; unchanged when it fits. One pass: each entry's serialized size (compact, as the
    /// body is written) is added to a running total, so the cut costs the node's size once, not once per entry kept.
    /// </summary>
    private static (JsonNode Node, bool Truncated) Cap(JsonNode node)
    {
        if (Size(node) <= CapBytes)
        {
            return (node, false);
        }

        return node is JsonObject obj ? (CapObject(obj), true) : (CapArray((JsonArray)node), true);
    }

    private static JsonObject CapObject(JsonObject obj)
    {
        var copy = new JsonObject();
        var used = 2;   // the braces
        foreach (var (key, value) in obj)
        {
            var entry = (copy.Count > 0 ? 1 : 0) + Size(JsonValue.Create(key)) + 1 + Size(value);   // comma, "key", colon, value
            if (used + entry > CapBytes)
            {
                break;
            }

            used += entry;
            copy[key] = value?.DeepClone();
        }

        return copy;
    }

    private static JsonArray CapArray(JsonArray array)
    {
        var kept = new JsonArray();
        var used = 2;   // the brackets
        foreach (var item in array)
        {
            var entry = (kept.Count > 0 ? 1 : 0) + Size(item);
            if (used + entry > CapBytes)
            {
                break;
            }

            used += entry;
            kept.Add(item?.DeepClone());
        }

        return kept;
    }

    private static int Size(JsonNode? node) => node is null ? 4 : Encoding.UTF8.GetByteCount(node.ToJsonString());   // 'null' is 4 bytes

    /// <summary>The text cut to the cap on a character boundary, in one pass over its characters against the byte budget.</summary>
    private static (string Text, bool Truncated) CutText(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) <= CapBytes)
        {
            return (text, false);
        }

        int bytes = 0, length = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > CapBytes)
            {
                break;
            }

            bytes += rune.Utf8SequenceLength;
            length += rune.Utf16SequenceLength;
        }

        return (text[..length], true);
    }
}
