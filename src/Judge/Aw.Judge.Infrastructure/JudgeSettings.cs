namespace Aw.Judge.Infrastructure;

/// <summary>Judge's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where Judge keeps its state: $AW_STATE_DIR/judge, or ~/.agentic-workflow/judge.</param>
internal sealed record JudgeSettings(string StateDirectory);
