using Aw.Transcripts.Core;

namespace Aw.Transcripts.Infrastructure;

/// <summary>Transcripts's infrastructure settings: the directories it reads (SettingsFromEnvironment builds the defaults).</summary>
/// <param name="Roots">The transcript roots, one provider each.</param>
internal sealed record TranscriptsSettings(IReadOnlyList<TranscriptRoot> Roots)
{
    /// <summary>The host's (worktree root, main checkout) pairs for cwds that no longer exist; empty by default.</summary>
    public IReadOnlyList<WorktreeRoot> WorktreeRoots { get; init; } = [];
}
