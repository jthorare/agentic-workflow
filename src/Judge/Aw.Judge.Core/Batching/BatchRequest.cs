using System.Text.Json.Nodes;

namespace Aw.Judge.Core.Batching;

/// <summary>Several questions about one state, asked in one call (the prompt sorter's eleven axes, a trait check's traits).</summary>
/// <param name="State">What the questions are about (the prompt, a diff); redacted and capped by the adapter before it leaves the machine.</param>
/// <param name="Questions">The questions, each with a key unique within the request.</param>
/// <param name="Budget">The time budget for the whole call, the key read included.</param>
public sealed record BatchRequest(JsonNode? State, IReadOnlyList<BatchQuestion> Questions, TimeSpan Budget);
