namespace Aw.Transcripts.Infrastructure;

/// <summary>Transcripts's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where Transcripts keeps its state: $AW_STATE_DIR/transcripts, or ~/.agentic-workflow/transcripts.</param>
internal sealed record TranscriptsSettings(string StateDirectory);
