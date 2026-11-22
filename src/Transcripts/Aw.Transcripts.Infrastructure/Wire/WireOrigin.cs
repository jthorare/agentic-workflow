namespace Aw.Transcripts.Infrastructure.Wire;

/// <summary>Where a Claude Code user line came from ('human', 'peer', 'task-notification', ...); older versions write none.</summary>
internal sealed record WireOrigin
{
    public string? Kind { get; init; }
}
