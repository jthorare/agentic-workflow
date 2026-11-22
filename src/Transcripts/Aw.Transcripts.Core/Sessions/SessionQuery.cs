namespace Aw.Transcripts.Core.Sessions;

/// <summary>Which sessions to list.</summary>
/// <param name="Since">Only files written at or after this time.</param>
public sealed record SessionQuery(DateTimeOffset Since)
{
    /// <summary>Only this provider's sessions, or every provider's when null.</summary>
    public TranscriptProvider? Provider { get; init; }

    /// <summary>Only sessions of this main repo (an exact match of <see cref="TranscriptSession.Repo"/>), or every repo's when null.</summary>
    public string? Repo { get; init; }

    /// <summary>Whether subagent sessions are listed too.</summary>
    public bool IncludeSubagents { get; init; } = true;
}
