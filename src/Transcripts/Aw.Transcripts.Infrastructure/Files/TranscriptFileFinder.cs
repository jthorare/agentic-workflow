using Aw.Transcripts.Core;

namespace Aw.Transcripts.Infrastructure.Files;

/// <summary>
/// Finds the transcript files under the roots by each provider's layout, without opening them:
/// Claude Code '&lt;project&gt;/&lt;session&gt;.jsonl' and '&lt;project&gt;/&lt;session&gt;/subagents/agent-&lt;id&gt;.jsonl';
/// Codex 'YYYY/MM/DD/rollout-*.jsonl'; Cursor '&lt;project&gt;/agent-transcripts/&lt;id&gt;/&lt;id&gt;.jsonl' and
/// '&lt;id&gt;/subagents/*.jsonl' (and the flat '&lt;project&gt;/agent-transcripts/&lt;id&gt;.jsonl').
/// A missing or unreadable directory adds no file.
/// </summary>
internal static class TranscriptFileFinder
{
    private const string Jsonl = "*.jsonl";

    /// <summary>Every transcript file under the roots.</summary>
    /// <param name="roots">The roots.</param>
    /// <returns>The files; a path under two roots is listed once.</returns>
    public static IReadOnlyList<TranscriptFile> Find(IEnumerable<TranscriptRoot> roots)
    {
        Dictionary<string, TranscriptFile> files = new(StringComparer.Ordinal);
        foreach (var root in roots)
        {
            var found = root.Provider switch
            {
                TranscriptProvider.ClaudeCode => Claude(root.Directory),
                TranscriptProvider.Codex => Files(root.Directory, "rollout-*.jsonl", SearchOption.AllDirectories).Select(p => (p, (string?)null)),
                TranscriptProvider.Cursor => Cursor(root.Directory),
                _ => [],
            };
            foreach (var (path, parent) in found)
            {
                if (ModifiedAt(path) is { } at)
                {
                    _ = files.TryAdd(path, new TranscriptFile(root.Provider, path, at, parent));
                }
            }
        }

        return [.. files.Values];
    }

    private static IEnumerable<(string Path, string? Parent)> Claude(string root)
    {
        foreach (var project in Directories(root))
        {
            foreach (var file in Files(project, Jsonl))
            {
                yield return (file, null);
            }

            foreach (var session in Directories(project))
            {
                var parent = Path.Combine(project, Path.GetFileName(session) + ".jsonl");
                foreach (var file in Files(Path.Combine(session, "subagents"), "agent-*.jsonl"))
                {
                    yield return (file, parent);
                }
            }
        }
    }

    private static IEnumerable<(string Path, string? Parent)> Cursor(string root)
    {
        foreach (var project in Directories(root))
        {
            var transcripts = Path.Combine(project, "agent-transcripts");
            foreach (var file in Files(transcripts, Jsonl))
            {
                yield return (file, null);
            }

            foreach (var session in Directories(transcripts))
            {
                var main = Path.Combine(session, Path.GetFileName(session) + ".jsonl");
                foreach (var file in Files(session, Jsonl))
                {
                    yield return (file, null);
                }

                foreach (var file in Files(Path.Combine(session, "subagents"), Jsonl))
                {
                    yield return (file, main);
                }
            }
        }
    }

    private static string[] Directories(string directory)
    {
        try
        {
            return Directory.Exists(directory) ? Directory.GetDirectories(directory) : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string[] Files(string directory, string pattern, SearchOption option = SearchOption.TopDirectoryOnly)
    {
        try
        {
            return Directory.Exists(directory)
                ? Directory.GetFiles(directory, pattern, new EnumerationOptions { RecurseSubdirectories = option == SearchOption.AllDirectories, IgnoreInaccessible = true, MatchCasing = MatchCasing.CaseSensitive })
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    // Null when the file went away between the listing and the stat.
    private static DateTimeOffset? ModifiedAt(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
