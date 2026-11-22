using System.Text.Json;

namespace Aw.Transcripts.Infrastructure.Wire;

/// <summary>A Claude Code or Cursor message: its content is a string or an array of blocks (ContentBlocks reads it).</summary>
internal sealed record WireMessage
{
    public string? Model { get; init; }

    public JsonElement? Content { get; init; }
}
