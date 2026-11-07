using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What a Step's launch is given beside its run, target and descriptor.</summary>
/// <param name="Instructions">What the Step is told to do, for the brief; none when it has none.</param>
/// <param name="Workspace">The workspace the Step reads (an existing worktree it works in); null when it reads none.</param>
/// <param name="Bundle">The pack the Step bundles (LN-WF5), resolved; null for none.</param>
/// <param name="Facts">The Step's declared read facts as files for its run directory, file name to content (written by the launcher); null for none.</param>
/// <param name="Model">The model the Step's 'model' parameter asked for; null runs the agent's default.</param>
/// <param name="BaseCommit">The commit the workspace started from (a phase workspace's seed), for the host-side prepare; null when the launcher should record the repo's HEAD at the run's first launch.</param>
public sealed record AgentLaunchExtras(IReadOnlyList<string>? Instructions = null, string? Workspace = null, PackBundle? Bundle = null, IReadOnlyDictionary<string, string>? Facts = null, string? Model = null, string? BaseCommit = null)
{
    /// <summary>Null, or the skill block this Step runs (skill flows spec D2): the launch gets its invocation as the whole seed instead of the Step brief.</summary>
    public Skills.SkillLaunch? Skill { get; init; }

    /// <summary>Sindri's own home, for the run directory a skill launch's seed names (<see cref="Launch.StepEnvironment"/>); null names '$AW_SINDRI_RUN_DIR'.</summary>
    public string? SindriHome { get; init; }

    /// <summary>Null, or the state's timeout, which goes to the launch request and from there to the sandbox plan's wall limit (skill flows spec D8).</summary>
    public TimeSpan? StepTimeout { get; init; }
}
