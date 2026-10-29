using System.Text.Json.Nodes;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>Reads the token counts an agent CLI prints, as it counted them; a CLI that gives none gives null (nothing is estimated here).</summary>
internal static class ProviderUsageReader
{
    /// <summary>
    /// The usage of a claude or cursor envelope: `usage.input_tokens` and `usage.output_tokens`, else the sums over `modelUsage`
    /// (`inputTokens`, `outputTokens`). The model is `modelUsage`'s key (the resolved id, 'claude-haiku-5-5' for 'haiku'); with several, the one that wrote most.
    /// </summary>
    /// <param name="envelope">The CLI's output envelope.</param>
    /// <returns>The usage, or null when the envelope has none.</returns>
    public static ProviderUsage? FromEnvelope(CliEnvelope envelope)
    {
        var model = ModelOf(envelope.ModelUsage);
        if (Counts(envelope.Usage, "input_tokens", "output_tokens") is { } counts)
        {
            return new ProviderUsage(counts.Input, counts.Output) { Model = model };
        }

        if (envelope.ModelUsage is null)
        {
            return null;
        }

        long totalIn = 0, totalOut = 0;
        var any = false;
        foreach (var entry in envelope.ModelUsage.Select(pair => Counts(pair.Value as JsonObject, "inputTokens", "outputTokens")))
        {
            if (entry is { } modelCounts)
            {
                totalIn += modelCounts.Input;
                totalOut += modelCounts.Output;
                any = true;
            }
        }

        return any ? new ProviderUsage(totalIn, totalOut) { Model = model } : null;
    }

    /// <summary>The usage of codex's `turn.completed` event: `input_tokens` and `output_tokens` (cached and reasoning counts are not added).</summary>
    /// <param name="usage">The event's `usage` object.</param>
    /// <returns>The usage, or null when it has none.</returns>
    public static ProviderUsage? FromCodex(JsonObject? usage) =>
        Counts(usage, "input_tokens", "output_tokens") is { } counts ? new ProviderUsage(counts.Input, counts.Output) : null;

    /// <summary>The result with the CLI's usage on it when it is an answer.</summary>
    /// <param name="result">What the provider read.</param>
    /// <param name="usage">The CLI's usage, if any.</param>
    /// <returns>The same result, or the decision with <see cref="ProviderDecided.Usage"/> set.</returns>
    public static ProviderResult Attach(ProviderResult result, ProviderUsage? usage) =>
        usage is not null && result is ProviderDecided decided ? decided with { Usage = usage } : result;

    private static (long Input, long Output)? Counts(JsonObject? node, string inputKey, string outputKey) =>
        node is not null && Count(node[inputKey]) is { } input && Count(node[outputKey]) is { } output ? (input, output) : null;

    private static long? Count(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<long>(out var count) && count >= 0 ? count : null;

    private static string? ModelOf(JsonObject? modelUsage) =>
        modelUsage?.OrderByDescending(pair => Count((pair.Value as JsonObject)?["outputTokens"]) ?? 0).Select(pair => pair.Key).FirstOrDefault();
}
