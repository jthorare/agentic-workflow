using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// One line of `codex exec --json`: `{"type":"item.completed","item":{"type":"agent_message","text":"{…}"}}`;
/// a rejected run prints `{"type":"error","message":"…","codex_error_info":"…"}` or `{"type":"turn.failed","error":{"message":"…"}}`;
/// a finished turn prints `{"type":"turn.completed","usage":{"input_tokens":…,"output_tokens":…}}`.
/// </summary>
/// <param name="Type">The event type.</param>
/// <param name="Item">The item, for item events.</param>
/// <param name="Usage">The token counts, for `turn.completed`.</param>
/// <param name="Message">The error text, for `error`.</param>
/// <param name="Error">The error, for `turn.failed`: an object with `message`, or the text itself.</param>
/// <param name="CodexErrorInfo">The error's kind (`usage_limit_exceeded`, …) when the event has one.</param>
internal sealed record CodexEvent(
    string? Type = null,
    CodexItem? Item = null,
    JsonObject? Usage = null,
    string? Message = null,
    JsonNode? Error = null,
    [property: JsonPropertyName("codex_error_info")] JsonNode? CodexErrorInfo = null);
