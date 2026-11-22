using Aw.Kernel;
using Aw.Transcripts.Core;
using Aw.Transcripts.Core.Ports;
using Aw.Transcripts.Core.Refs;
using Aw.Transcripts.Core.Sessions;

namespace Aw.Transcripts.Testing;

/// <summary>The in-memory <see cref="ITranscriptCorpus"/>: it holds finished transcripts (already deduped and scrubbed) and filters them like the file corpus.</summary>
/// <param name="transcripts">The sessions it holds.</param>
public sealed class InMemoryTranscriptCorpus(IEnumerable<SessionTranscript> transcripts) : ITranscriptCorpus
{
    private readonly List<SessionTranscript> _transcripts = [.. transcripts];

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<TranscriptSession>>> ListSessions(SessionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IReadOnlyList<TranscriptSession> sessions =
        [
            .. _transcripts.Select(t => t.Session)
                .Where(s => s.LastWrittenAt >= query.Since
                    && (query.Provider is not { } p || s.Provider == p)
                    && (query.IncludeSubagents || !s.IsSubagent)
                    && (query.Repo is null || string.Equals(s.Repo, query.Repo, StringComparison.Ordinal)))
                .OrderByDescending(s => s.LastWrittenAt)
                .ThenBy(s => s.Ref.Value, StringComparer.Ordinal),
        ];
        return ValueTask.FromResult(Results.Success(sessions));
    }

    /// <inheritdoc />
    public ValueTask<Result<SessionTranscript>> ReadSession(SessionRef session, CancellationToken cancellationToken)
    {
        var found = _transcripts.Where(t => t.Session.Ref == session).ToList();
        Result<SessionTranscript> result = found.Count switch
        {
            0 => TranscriptsErrors.SessionNotFound.With($"no transcript has the ref {session.Value}"),
            1 => found[0],
            _ => TranscriptsErrors.AmbiguousRef.With($"two transcripts share the ref {session.Value}"),
        };
        return ValueTask.FromResult(result);
    }
}
