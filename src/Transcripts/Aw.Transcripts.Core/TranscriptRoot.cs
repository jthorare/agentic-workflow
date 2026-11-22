namespace Aw.Transcripts.Core;

/// <summary>A directory that holds one provider's transcripts, such as ~/.claude/projects or a Sindri agent home's.</summary>
/// <param name="Provider">The provider whose file layout the directory has.</param>
/// <param name="Directory">The absolute directory. A missing one holds no sessions; it is not an error.</param>
public sealed record TranscriptRoot(TranscriptProvider Provider, string Directory);
