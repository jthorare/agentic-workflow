namespace Aw.Transcripts.Infrastructure.Wire;

/// <summary>The fields of a Claude Code transcript line the reader uses; every other field is ignored.</summary>
internal sealed record ClaudeLine
{
    public string? Type { get; init; }

    public string? Timestamp { get; init; }

    public string? Cwd { get; init; }

    public bool? IsMeta { get; init; }

    public bool? IsSidechain { get; init; }

    public bool? IsCompactSummary { get; init; }

    public bool? IsVisibleInTranscriptOnly { get; init; }

    public string? Entrypoint { get; init; }

    public WireOrigin? Origin { get; init; }

    public WireMessage? Message { get; init; }
}
