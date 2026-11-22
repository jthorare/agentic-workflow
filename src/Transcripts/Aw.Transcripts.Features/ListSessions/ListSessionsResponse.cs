using Aw.Transcripts.Core.Sessions;

namespace Aw.Transcripts.Features.ListSessions;

/// <summary>The ListSessions response.</summary>
/// <param name="Sessions">The sessions, newest first.</param>
public sealed record ListSessionsResponse(IReadOnlyList<TranscriptSession> Sessions);
