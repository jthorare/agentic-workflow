using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Transcripts;
using Aw.Kernel;

namespace Aw.Judge.Testing;

/// <summary>The in-memory ITranscriptReader fake: transcripts added by path and session; it passes TranscriptReaderContract.</summary>
public sealed class InMemoryTranscriptReader : ITranscriptReader
{
    private readonly Dictionary<string, Transcript> _byPath = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _pathBySession = new(StringComparer.Ordinal);

    /// <summary>Adds a transcript.</summary>
    /// <param name="session">The session whose file it is.</param>
    /// <param name="path">The file path.</param>
    /// <param name="transcript">What reading the file gives.</param>
    /// <returns>This reader.</returns>
    public InMemoryTranscriptReader Add(SessionId session, string path, Transcript transcript)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(transcript);
        _byPath[path] = transcript;
        _pathBySession[session.Value] = path;
        return this;
    }

    /// <inheritdoc />
    public ValueTask<Result<Transcript>> FindSession(SessionId session, CancellationToken cancellationToken) =>
        _pathBySession.TryGetValue(session.Value, out var path)
            ? Read(path, cancellationToken)
            : Missing($"no transcript for session '{session.Value}'");

    /// <inheritdoc />
    public ValueTask<Result<Transcript>> Read(string path, CancellationToken cancellationToken) =>
        _byPath.TryGetValue(path, out var transcript)
            ? ValueTask.FromResult(Results.Success(transcript))
            : Missing($"can't read the transcript '{path}'");

    private static ValueTask<Result<Transcript>> Missing(string detail) =>
        ValueTask.FromResult<Result<Transcript>>(JudgeErrors.NoTranscript.With(detail));
}
