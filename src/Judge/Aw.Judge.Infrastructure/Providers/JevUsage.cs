using System.Text.Json.Serialization;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>What the call cost, in the API's snake_case fields.</summary>
/// <param name="InputTokens">Tokens read.</param>
/// <param name="OutputTokens">Tokens written.</param>
internal sealed record JevUsage(
    [property: JsonPropertyName("input_tokens")] double? InputTokens = null,
    [property: JsonPropertyName("output_tokens")] double? OutputTokens = null);
