using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// The one JSON object `claude -p --output-format json` and `cursor-agent -p --output-format json` print:
/// `{"type":"result","is_error":false,"result":"…","structured_output":{…}}` (structured_output only from claude,
/// when given --json-schema).
/// </summary>
/// <param name="StructuredOutput">The schema-checked answer (claude with --json-schema).</param>
/// <param name="Result">The model's final text.</param>
/// <param name="IsError">Whether the CLI reports the run failed.</param>
internal sealed record CliEnvelope(
    [property: JsonPropertyName("structured_output")] JsonNode? StructuredOutput = null,
    [property: JsonPropertyName("result")] string? Result = null,
    [property: JsonPropertyName("is_error")] bool? IsError = null);
