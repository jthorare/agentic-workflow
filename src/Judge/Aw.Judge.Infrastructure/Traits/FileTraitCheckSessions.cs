using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;

namespace Aw.Judge.Infrastructure.Traits;

/// <summary>
/// ITraitCheckSessions over '&lt;judge dir&gt;/trait-check/sessions/&lt;sid&gt;.repos': one repo root per line, appended the
/// first time the session edits a file in that repo; a session id that can't be a file name records nothing. Two edits
/// racing may both append a repo; reading drops the repeat. The first record of a session sweeps the records nobody wrote
/// to for a week.
/// </summary>
/// <param name="directory">'&lt;judge dir&gt;/trait-check/sessions'.</param>
/// <param name="clock">When a record is old enough to sweep.</param>
internal sealed class FileTraitCheckSessions(string directory, TimeProvider clock) : ITraitCheckSessions
{
    private static readonly TimeSpan _keep = TimeSpan.FromDays(7);
    private const int MaxIdLength = 80;

    /// <inheritdoc />
    public void Add(SessionId sessionId, string repoRoot)
    {
        if (PathOf(sessionId) is not { } path)
        {
            return;
        }

        try
        {
            if (!File.Exists(path))
            {
                Sweep();
            }
            else if (Repos(sessionId).Contains(repoRoot, StringComparer.Ordinal))
            {
                return;
            }

            _ = Directory.CreateDirectory(directory);
            File.AppendAllText(path, repoRoot + "\n");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The record is best effort: without it the Stop check judges the working directory's repo only.
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Repos(SessionId sessionId)
    {
        try
        {
            return PathOf(sessionId) is { } path && File.Exists(path) ? [.. File.ReadAllLines(path).Where(line => line.Length > 0).Distinct(StringComparer.Ordinal)] : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    // Letters, digits, '.', '_' and '-', at most 80, no '..': the ids the sorter's session files accept too.
    private string? PathOf(SessionId session) =>
        session.Value is { Length: >= 1 and <= MaxIdLength } id && id.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-') && !id.Contains("..", StringComparison.Ordinal)
            ? Path.Combine(directory, id + ".repos")
            : null;

    private void Sweep()
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        var cutoff = clock.GetUtcNow().UtcDateTime - _keep;
        foreach (var record in Directory.EnumerateFiles(directory).Where(record => File.GetLastWriteTimeUtc(record) < cutoff))
        {
            File.Delete(record);
        }
    }
}
