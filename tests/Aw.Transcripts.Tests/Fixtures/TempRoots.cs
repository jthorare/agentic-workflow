using Aw.Redaction;
using Aw.Transcripts.Core;
using Aw.Transcripts.Infrastructure;
using Aw.Transcripts.Infrastructure.Reading;

namespace Aw.Transcripts.Tests.Fixtures;

/// <summary>A throwaway directory holding a Claude, a Codex and a Cursor root, and the file corpus over them.</summary>
internal sealed class TempRoots : IDisposable
{
    public TempRoots()
    {
        Root = Directory.CreateTempSubdirectory("aw-transcripts-").FullName;
        Claude = Path.Combine(Root, "claude", "projects");
        Codex = Path.Combine(Root, "codex", "sessions");
        Cursor = Path.Combine(Root, "cursor", "projects");
    }

    public string Root { get; }

    public string Claude { get; }

    public string Codex { get; }

    public string Cursor { get; }

    public IReadOnlyList<TranscriptRoot> Roots =>
    [
        new(TranscriptProvider.ClaudeCode, Claude),
        new(TranscriptProvider.Codex, Codex),
        new(TranscriptProvider.Cursor, Cursor),
    ];

    public FileTranscriptCorpus Corpus(IReadOnlyList<WorktreeRoot>? worktreeRoots = null) =>
        new(new TranscriptsSettings(Roots) { WorktreeRoots = worktreeRoots ?? [] }, new TranscriptFileReader(Redactor.Default));

    /// <summary>Writes a file under the root (directories made) and sets its write time.</summary>
    public string Write(string relative, DateTimeOffset writtenAt, params string[] lines)
    {
        var path = Path.Combine(Root, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!); // why: a path under Root always has a parent
        File.WriteAllText(path, string.Join('\n', lines) + "\n");
        File.SetLastWriteTimeUtc(path, writtenAt.UtcDateTime);
        return path;
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
