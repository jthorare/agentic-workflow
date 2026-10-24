using System.Text.Json.Nodes;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.PromptSort;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Judge.Infrastructure.PromptSort;

/// <summary>
/// ISortSessionStore over 'judge/sessions/&lt;sid&gt;.sort.json'. The file is a hook contract: done-gate.sh reads
/// 'requirements.uiEvidence' from it and then rewrites it with jq, so a write keeps every key this build doesn't know and
/// replaces the file whole (a temp file renamed over it). A file that can't be read as a state is the empty state, so a
/// file a TS sorter left behind reads as is and a damaged one costs only the cooldown.
/// </summary>
/// <param name="directory">The judge's 'sessions' directory ($AW_JUDGE_STATE_DIR/sessions, created on the first write).</param>
internal sealed class FileSortSessionStore(string directory) : ISortSessionStore
{
    private const int MaxIdLength = 80;

    /// <inheritdoc />
    public async ValueTask<Result<SortSession>> Read(SessionId session, CancellationToken cancellationToken)
    {
        if (!IsFileName(session.Value))
        {
            return SortSession.Empty;
        }

        try
        {
            var text = await File.ReadAllTextAsync(PathOf(session), cancellationToken).ConfigureAwait(false);
            return SortSessionFile.Read(JsonNode.Parse(text));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return SortSession.Empty;
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<Unit>> Write(SessionId session, SortSession state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!IsFileName(session.Value))
        {
            return JudgeErrors.Invalid.With($"session id can't be a file name: {session.Value}");
        }

        var file = PathOf(session);
        var temporary = $"{file}.{Guid.NewGuid():N}.tmp";
        try
        {
            _ = Directory.CreateDirectory(directory);
            var existing = await Existing(file, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(temporary, SortSessionFile.Merge(existing, state).ToJsonString(), cancellationToken).ConfigureAwait(false);
            File.Move(temporary, file, overwrite: true);
            return Results.Ok();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return DataErrors.Unavailable.With($"Can't write the sorter state '{file}': {exception.Message}");
        }
        finally
        {
            Discard(temporary);
        }
    }

    // Letters, digits, '.', '_' and '-', at most 80, no '..': the character set done-gate.sh accepts too.
    private static bool IsFileName(string id) =>
        id.Length is >= 1 and <= MaxIdLength && id.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-') && !id.Contains("..", StringComparison.Ordinal);

    private string PathOf(SessionId session) => Path.Combine(directory, session.Value + ".sort.json");

    private static async ValueTask<JsonObject?> Existing(string file, CancellationToken cancellationToken)
    {
        try
        {
            return JsonNode.Parse(await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false)) as JsonObject;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static void Discard(string temporary)
    {
        try
        {
            File.Delete(temporary);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // why: best effort; a successful write already moved it.
        }
    }
}
