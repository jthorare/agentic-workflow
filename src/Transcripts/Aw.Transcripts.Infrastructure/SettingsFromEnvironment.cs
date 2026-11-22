using Aw.Transcripts.Core;

namespace Aw.Transcripts.Infrastructure;

/// <summary>
/// The default roots, from the environment Composition passes in: each provider's home under HOME
/// (~/.claude/projects, ~/.codex/sessions, ~/.cursor/projects) and Sindri's separate agent homes
/// ($AW_SINDRI_HOME/agent-homes/claude-code/projects and .../codex/sessions; AW_SINDRI_HOME defaults to
/// $AW_STATE_DIR/sindri-app, AW_STATE_DIR to ~/.agentic-workflow). A root that doesn't exist holds no sessions.
/// </summary>
internal static class SettingsFromEnvironment
{
    internal static TranscriptsSettings Read(Func<string, string?> environment)
    {
        var home = environment("HOME") is { Length: > 0 } h ? h : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var state = environment("AW_STATE_DIR") is { Length: > 0 } s ? s : Path.Combine(home, ".agentic-workflow");
        var sindri = environment("AW_SINDRI_HOME") is { Length: > 0 } a ? a : Path.Combine(state, "sindri-app");
        var agents = Path.Combine(sindri, "agent-homes");
        return new TranscriptsSettings(
        [
            new TranscriptRoot(TranscriptProvider.ClaudeCode, Path.Combine(home, ".claude", "projects")),
            new TranscriptRoot(TranscriptProvider.Codex, Path.Combine(home, ".codex", "sessions")),
            new TranscriptRoot(TranscriptProvider.Cursor, Path.Combine(home, ".cursor", "projects")),
            new TranscriptRoot(TranscriptProvider.ClaudeCode, Path.Combine(agents, "claude-code", "projects")),
            new TranscriptRoot(TranscriptProvider.Codex, Path.Combine(agents, "codex", "sessions")),
        ]);
    }
}
