using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The one place a node run's stored Input and Output become text that always parses as JSON (#3419): a 64 KB cut becomes a small object, a redactor block becomes a JSON string.
/// </summary>
internal static class StoredJson
{
    /// <summary>What the redactor leaves in place of text it blocked.</summary>
    internal const string BlockedMarker = "[blocked by the redactor]";

    private const int Slack = 128;   // why: room for the envelope's own keys and the byte count

    /// <summary>The blocked marker as a JSON string.</summary>
    internal static string Blocked => JsonValue.Create(BlockedMarker).ToJsonString();

    /// <summary>Turns a scrubbed text that is the bare marker into the JSON string; any other text stays.</summary>
    /// <param name="cleaned">The text after the redactor.</param>
    /// <returns>Text that parses as JSON when the input did.</returns>
    internal static string? Quote(string? cleaned) => string.Equals(cleaned, BlockedMarker, StringComparison.Ordinal) ? Blocked : cleaned;

    /// <summary>Parses stored text; null when it is null or not JSON.</summary>
    /// <param name="text">The stored text.</param>
    /// <returns>The node, or null.</returns>
    internal static JsonNode? Parse(string? text)
    {
        try
        {
            return text is null ? null : JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>True when the node is the object <see cref="Fit"/> writes for a text over the cap. Only for reading a stored output back in a scope; a writer uses <see cref="Fitted.Cut"/>.</summary>
    /// <param name="node">The parsed stored text.</param>
    /// <returns>Whether it is a cut envelope.</returns>
    internal static bool IsCut(JsonNode? node) =>
        node is JsonObject o && o.ContainsKey("head") && o["truncated"] is JsonValue v && v.TryGetValue<bool>(out var cut) && cut;

    /// <summary>True when the node is the JSON string <see cref="Blocked"/> writes.</summary>
    /// <param name="node">The parsed stored text.</param>
    /// <returns>Whether the redactor blocked it.</returns>
    internal static bool IsBlocked(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) && string.Equals(s, BlockedMarker, StringComparison.Ordinal);

    /// <summary>Why a reference read a value that is not there: a node's output was cut at 64 KB or blocked by the redactor. The one place both the block and the run_agent paths ask.</summary>
    /// <param name="scope">The scope the node's references were read through.</param>
    /// <param name="loopCutNode">The node whose cut output hid a loop's list, or null.</param>
    /// <returns>The failure, or null when every reference read a value.</returns>
    internal static Kernel.Error? ReadFailure(GraphNodeScope scope, string? loopCutNode)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var cut = loopCutNode ?? scope.Cut;
        if (cut is not null)
        {
            return SindriErrors.GraphOutputTruncated.With($"the output of '{cut}' was cut at 64 KB; refer to a smaller field");
        }

        return scope.Blocked is { } blocked ? SindriErrors.GraphOutputTruncated.With($"the output of '{blocked}' was blocked by the redactor") : null;
    }

    /// <summary>
    /// Keeps a block's input or output under Temporal's 2 MB payload limit and the store's 64 KB cap. A text over the cap becomes
    /// {"truncated":true,"bytes":N,"head":"first part"}, which is under the cap and parses.
    /// </summary>
    /// <param name="text">The input or output text, or null.</param>
    /// <returns>The text when it fits, otherwise the envelope, and whether it was cut.</returns>
    internal static Fitted Fit(string? text)
    {
        if (text is null)
        {
            return new Fitted(Text: null, Cut: false);
        }

        var bytes = Encoding.UTF8.GetByteCount(text);
        if (bytes <= NodeRunLimits.PayloadBytes)
        {
            return new Fitted(text, Cut: false);
        }

        var length = Math.Min(text.Length, NodeRunLimits.PayloadBytes - Slack);
        while (true)
        {
            var head = length > 0 && char.IsHighSurrogate(text[length - 1]) ? length - 1 : length;
            var envelope = new JsonObject { ["truncated"] = true, ["bytes"] = bytes, ["head"] = text[..head] }.ToJsonString();
            var size = Encoding.UTF8.GetByteCount(envelope);
            if (size <= NodeRunLimits.PayloadBytes || head == 0)
            {
                return new Fitted(envelope, Cut: true);
            }

            length = head - ((size - NodeRunLimits.PayloadBytes) / 2) - 1;   // why: escaping can make the head longer than its text; shrink by the excess
            length = Math.Max(length, 0);
        }
    }
}
