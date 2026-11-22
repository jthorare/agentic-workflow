using Aw.Transcripts.Core;

namespace Aw.Transcripts.Infrastructure;

/// <summary>Transcripts's infrastructure settings: the directories it reads (SettingsFromEnvironment builds the defaults).</summary>
/// <param name="Roots">The transcript roots, one provider each.</param>
internal sealed record TranscriptsSettings(IReadOnlyList<TranscriptRoot> Roots);
