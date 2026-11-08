using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// IClaudeTranscripts over ~/.claude/projects: the transcript of a session is &lt;project&gt;/&lt;session&gt;.jsonl in whichever project directory
/// holds it. Only the last bytes are read, and the file is opened for sharing because Claude Code is appending to it.
/// </summary>
/// <param name="projects">The projects directory.</param>
internal sealed class ClaudeProjectsTranscripts(string projects) : IClaudeTranscripts
{
    public async ValueTask<Result<TranscriptTail?>> Tail(SessionId session, int maxBytes, CancellationToken cancellationToken)
    {
        var id = session.Value;
        if (id.Length == 0 || id.Any(static c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')) || id.All(static c => c == '.'))
        {
            return Results.Success<TranscriptTail?>(value: null);   // not a name that could be a transcript file
        }

        try
        {
            var path = Find(id + ".jsonl");
            return path is null ? Results.Success<TranscriptTail?>(value: null) : await TailFile.Read(path, maxBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return MetaErrors.TranscriptUnreadable.With($"{id}: {exception.Message}");
        }
    }

    // The newest match, when a session id turns up in more than one project.
    private string? Find(string file) =>
        Directory.Exists(projects)
            ? Directory.EnumerateDirectories(projects).Select(d => Path.Combine(d, file)).Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
            : null;
}
