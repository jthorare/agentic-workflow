using Aw.Kernel;
using Aw.Transcripts.Core;
using Aw.Transcripts.Core.Ports;
using Aw.Transcripts.Core.Refs;
using Aw.Transcripts.Core.Sessions;
using Aw.Transcripts.Infrastructure.Files;
using Aw.Transcripts.Infrastructure.Reading;
using Aw.Transcripts.Infrastructure.Repos;

namespace Aw.Transcripts.Infrastructure;

/// <summary>
/// <see cref="ITranscriptCorpus"/> over the transcript files under the roots. A list stats every file and reads only
/// the ones written since the time asked (plus, for dedupe, the files beside them that were still being written when
/// they started: see <see cref="SessionDedupe"/>). A ref resolves by hashing every file's path, without opening one.
/// </summary>
/// <param name="settings">The roots.</param>
/// <param name="reader">Reads one file.</param>
internal sealed class FileTranscriptCorpus(TranscriptsSettings settings, TranscriptFileReader reader) : ITranscriptCorpus
{
    private readonly RepoResolver _repos = new();

    public async ValueTask<Result<IReadOnlyList<TranscriptSession>>> ListSessions(SessionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var all = TranscriptFileFinder.Find(settings.Roots).Where(f => query.Provider is not { } p || f.Provider == p).ToList();
        var dedupe = new SessionDedupe(reader, all);
        List<TranscriptSession> sessions = [];
        foreach (var file in all.Where(f => f.ModifiedAt >= query.Since && (query.IncludeSubagents || !f.IsSubagentByPlace)))
        {
            // An unreadable file (deleted or locked mid-scan) is left out of the list, not an error for the rest.
            if (await dedupe.Turns(file, withTurns: false, cancellationToken).ConfigureAwait(false) is { IsSuccess: true } read)
            {
                sessions.Add(Session(file, read.Value.Parsed, read.Value.HumanTurns, all));
            }
        }

        return sessions
            .Where(s => (query.IncludeSubagents || !s.IsSubagent) && (query.Repo is null || string.Equals(s.Repo, query.Repo, StringComparison.Ordinal)))
            .OrderByDescending(s => s.LastWrittenAt)
            .ThenBy(s => s.Ref.Value, StringComparer.Ordinal)
            .ToList();
    }

    public async ValueTask<Result<SessionTranscript>> ReadSession(SessionRef session, CancellationToken cancellationToken)
    {
        var all = TranscriptFileFinder.Find(settings.Roots);
        var matches = all.Where(f => f.Ref == session).Take(2).ToList();
        if (matches.Count == 0)
        {
            return TranscriptsErrors.SessionNotFound.With($"no transcript under the roots has the ref {session.Value}");
        }

        if (matches.Count > 1)
        {
            return TranscriptsErrors.AmbiguousRef.With($"two transcript files share the ref {session.Value}");
        }

        var file = matches[0];
        var read = await new SessionDedupe(reader, all).Turns(file, withTurns: true, cancellationToken).ConfigureAwait(false);
        return read.Map(r => new SessionTranscript(Session(file, r.Parsed, r.HumanTurns, all), r.Turns, r.Parsed.SkippedLines));
    }

    private TranscriptSession Session(TranscriptFile file, ParsedFile parsed, int humanTurns, IReadOnlyList<TranscriptFile> all)
    {
        var parent = ParentOf(file, parsed, all);
        return new TranscriptSession(
            file.Ref,
            file.Provider,
            parsed.First ?? file.ModifiedAt,
            file.ModifiedAt,
            parsed.Cwd is { } cwd ? _repos.Resolve(cwd) : null,
            file.IsSubagentByPlace || parsed.IsSubagent,
            parent?.Ref,
            humanTurns);
    }

    // The parent's file: by place for Claude Code and Cursor, by the parent thread id in a Codex rollout's name.
    private static TranscriptFile? ParentOf(TranscriptFile file, ParsedFile parsed, IReadOnlyList<TranscriptFile> all)
    {
        if (file.ParentPath is { } path)
        {
            return all.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.Ordinal));
        }

        return parsed.ParentThread is { Length: > 0 } thread
            ? all.FirstOrDefault(f => f.Provider == TranscriptProvider.Codex && f.Path.EndsWith($"-{thread}.jsonl", StringComparison.Ordinal))
            : null;
    }
}
