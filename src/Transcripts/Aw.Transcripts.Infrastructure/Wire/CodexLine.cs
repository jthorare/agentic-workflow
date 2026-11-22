namespace Aw.Transcripts.Infrastructure.Wire;

/// <summary>A Codex rollout line: '{ timestamp, type, payload }'.</summary>
internal sealed record CodexLine
{
    public string? Timestamp { get; init; }

    public string? Type { get; init; }

    public CodexPayload? Payload { get; init; }
}
