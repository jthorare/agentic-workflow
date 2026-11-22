using System.Text.Json;

namespace Aw.Transcripts.Infrastructure.Wire;

/// <summary>
/// The payload fields the reader uses, across the line types: session_meta (id, cwd, source), event_msg user_message
/// (message), response_item message (role, content) and the tool calls (name).
/// </summary>
internal sealed record CodexPayload
{
    public string? Type { get; init; }

    public string? Id { get; init; }

    public string? Cwd { get; init; }

    public JsonElement? Source { get; init; }

    public JsonElement? Message { get; init; }

    public string? Role { get; init; }

    public string? Name { get; init; }

    public JsonElement? Content { get; init; }
}
