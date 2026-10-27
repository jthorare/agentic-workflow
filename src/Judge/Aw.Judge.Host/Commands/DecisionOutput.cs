using System.Text.Json.Serialization;
using Aw.Judge.Core.Decisions;

namespace Aw.Judge.Host.Commands;

/// <summary>What 'judge why|undo|approve' print: the decision row, with the decisions table's snake_case column names.</summary>
internal sealed record DecisionOutput(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("ts")] DateTimeOffset Ts,
    [property: JsonPropertyName("question")] string Question,
    [property: JsonPropertyName("content_class")] string ContentClass,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("decision")] string? Decision,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("reason_code")] string ReasonCode,
    [property: JsonPropertyName("latency_ms")] long LatencyMs,
    [property: JsonPropertyName("input_digest")] string InputDigest,
    [property: JsonPropertyName("undone_at")] DateTimeOffset? UndoneAt,
    [property: JsonPropertyName("chain_position")] int ChainPosition,
    [property: JsonPropertyName("skipped")] IReadOnlyList<SkippedProvider> Skipped,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("approved_at")] DateTimeOffset? ApprovedAt,
    [property: JsonPropertyName("reasons")] IReadOnlyList<string>? Reasons)
{
    internal static DecisionOutput From(DecisionRecord d) => new(
        d.Id.Value, d.Ts, d.Question, d.ContentClass, d.Provider, d.Decision, d.Confidence, d.ReasonCode, d.LatencyMs,
        d.InputDigest, d.UndoneAt, d.ChainPosition, d.Skipped,
        Frozen.Text(d.Outcome), d.ApprovedAt, d.Details?.Reasons);
}
