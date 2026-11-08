using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>ICodexRollouts over ~/.codex/sessions: the rollout of a session is rollout-*-&lt;session&gt;.jsonl under a year/month/day folder. Only the last bytes are read.</summary>
/// <param name="sessions">The sessions directory.</param>
internal sealed class CodexSessionRollouts(string sessions) : ICodexRollouts
{
    public async ValueTask<Result<TranscriptTail?>> Tail(SessionId session, int maxBytes, CancellationToken cancellationToken)
    {
        var id = session.Value;
        if (id.Length == 0 || id.Any(static c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
        {
            return Results.Success<TranscriptTail?>(value: null);   // not a name that could be in a rollout file name, and no wildcard reaches the search
        }

        try
        {
            var path = Directory.Exists(sessions)
                ? Directory.EnumerateFiles(sessions, $"rollout-*-{id}.jsonl", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
                : null;
            return path is null ? Results.Success<TranscriptTail?>(value: null) : Results.Success<TranscriptTail?>(await TailFile.Read(path, maxBytes, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return MetaErrors.TranscriptUnreadable.With($"{id}: {exception.Message}");
        }
    }
}
