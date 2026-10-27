namespace Aw.Judge.Infrastructure;

/// <summary>Judge's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="ProjectsDirectory">Where Claude keeps its session transcripts: $AW_JUDGE_PROJECTS_DIR, else ~/.claude/projects.</param>
/// <param name="StateDirectory">Where Judge keeps its state: $AW_JUDGE_STATE_DIR, else $AW_STATE_DIR/judge, else ~/.agentic-workflow/judge.</param>
internal sealed record JudgeSettings(string StateDirectory, string ProjectsDirectory);
