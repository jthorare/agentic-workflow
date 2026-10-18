using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>
/// Transcript files, only stat'ed (collector spec §5.2, §5.3): the modification time of a path the state file or an open file link gave, else of the files the
/// catalog's 'transcripts' glob finds for the session id. A transcript's content is never opened.
/// </summary>
/// <param name="home">What '~' means in a catalog glob.</param>
internal sealed class TranscriptFiles(string home) : ITranscripts
{
    private const int MaxMatches = 32;

    public DateTimeOffset? LastWrite(AgentEntry agent, SessionId? session, string? hinted)
    {
        ArgumentNullException.ThrowIfNull(agent);
        if (hinted is not null && Stat(hinted) is { } direct)
        {
            return direct;
        }

        if (agent.Transcripts is not { } glob || session is not { } id)
        {
            return null;
        }

        var pattern = glob.Replace("{sessionId}", id.Value, StringComparison.Ordinal);
        pattern = pattern.StartsWith('~') ? home + pattern[1..] : pattern;
        return Expand(pattern).Take(MaxMatches).Select(Stat).OfType<DateTimeOffset>().OrderDescending().Cast<DateTimeOffset?>().FirstOrDefault();
    }

    private static IEnumerable<string> Expand(string pattern)
    {
        IEnumerable<string> level = ["/"];
        foreach (var segment in pattern.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            level = segment.Contains('*', StringComparison.Ordinal) || segment.Contains('?', StringComparison.Ordinal)
                ? level.SelectMany(dir => Children(dir, segment))
                : level.Select(dir => Path.Combine(dir, segment));
        }

        return level;
    }

    private static IEnumerable<string> Children(string directory, string pattern)
    {
        try
        {
            return [.. Directory.EnumerateFileSystemEntries(directory, pattern).Take(MaxMatches)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static DateTimeOffset? Stat(string path)
    {
        try
        {
            return File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
