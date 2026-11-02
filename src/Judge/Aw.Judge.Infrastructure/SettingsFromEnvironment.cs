namespace Aw.Judge.Infrastructure;

/// <summary>Reads this package's settings from the environment that Composition passes in.</summary>
internal static class SettingsFromEnvironment
{
    /// <summary>
    /// AW_JUDGE_STATE_DIR is the judge directory itself (a caller that runs its own judge, with its own store and
    /// config, points it there; nothing else moves); else $AW_STATE_DIR/judge, else ~/.agentic-workflow/judge. AW_JUDGE_PROJECTS_DIR is the Claude transcripts directory, else ~/.claude/projects.
    /// </summary>
    internal static JudgeSettings Read(Func<string, string?> environment)
        => new(
            StateDirectory: environment("AW_JUDGE_STATE_DIR") is { Length: > 0 } judgeDirectory
                ? judgeDirectory
                : environment("AW_STATE_DIR") is { Length: > 0 } root
                    ? Path.Combine(root, "judge")
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow", "judge"),
            ProjectsDirectory: environment("AW_JUDGE_PROJECTS_DIR") is { Length: > 0 } projects
                ? projects
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects"));
}
