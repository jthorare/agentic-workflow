using Aw.Transcripts.Core;

namespace Aw.Transcripts.Features.ListSessions;

/// <summary>The ListSessions request: the sessions written since a time.</summary>
/// <param name="Since">Only files written at or after this time.</param>
public sealed record ListSessionsRequest(DateTimeOffset Since)
{
    /// <summary>Only this provider's sessions, or every provider's when null.</summary>
    public TranscriptProvider? Provider { get; init; }

    /// <summary>Only sessions of this main repo (an absolute path), or every repo's when null.</summary>
    public string? Repo { get; init; }

    /// <summary>Whether subagent sessions are listed too.</summary>
    public bool IncludeSubagents { get; init; } = true;
}
