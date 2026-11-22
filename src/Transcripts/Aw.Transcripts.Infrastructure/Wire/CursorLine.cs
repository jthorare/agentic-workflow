namespace Aw.Transcripts.Infrastructure.Wire;

/// <summary>A Cursor agent-transcript line: '{ role, message: { content } }' or '{ type: "turn_ended", status }'. It has no id and no timestamp.</summary>
internal sealed record CursorLine
{
    public string? Role { get; init; }

    public string? Type { get; init; }

    public WireMessage? Message { get; init; }
}
