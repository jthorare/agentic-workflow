namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The paths of one launch.</summary>
/// <param name="Repo">The repo checkout.</param>
/// <param name="GitCommonDir">The repo's git directory (the common one).</param>
/// <param name="Worktree">The run's worktree (the cwd), or null for a session with none.</param>
/// <param name="RunDirectory">The run directory (SB17).</param>
/// <param name="RunArea">The launcher's per-run area: scratch, shims, hook spool directories, the plan.</param>
public sealed record SandboxPaths(string Repo, string GitCommonDir, string? Worktree, string RunDirectory, string RunArea);
