using Aw.Transcripts.Core;
using Aw.Transcripts.Core.Refs;

namespace Aw.Transcripts.Infrastructure.Files;

/// <summary>One transcript file under a root.</summary>
/// <param name="Provider">The provider whose layout the root has.</param>
/// <param name="Path">The file's full path, its identity.</param>
/// <param name="ModifiedAt">Its last write time.</param>
/// <param name="ParentPath">For a subagent file known by its place (Claude Code, Cursor): the parent session's file; null otherwise.</param>
internal sealed record TranscriptFile(TranscriptProvider Provider, string Path, DateTimeOffset ModifiedAt, string? ParentPath)
{
    /// <summary>The file's opaque ref. A method, not a property: a typed-id property must be required on read (ARCH-ID-002), and this one is derived.</summary>
    /// <returns>The ref.</returns>
    public SessionRef SessionOf() => TranscriptRefs.ForFile(Provider, Path);

    /// <summary>The directory the file sits in: resumed and forked copies of a session sit beside it, so dedupe runs per directory.</summary>
    public string Directory => System.IO.Path.GetDirectoryName(Path) ?? Path;

    /// <summary>Whether the file's place says it is a subagent's.</summary>
    public bool IsSubagentByPlace => ParentPath is not null;
}
