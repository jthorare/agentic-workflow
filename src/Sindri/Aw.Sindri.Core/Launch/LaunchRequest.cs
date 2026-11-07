using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// One launch for <see cref="ILauncher"/> (API P10a; the shape engine P8 calls with, agreed 2026-10-16). The id is the engine's
/// 'LCH-…' <see cref="LaunchId"/>, not the collector's looser one of this namespace.
/// </summary>
/// <param name="Launch">The launch id; a second call with the same id returns the first call's session.</param>
/// <param name="Item">The item the session works on.</param>
/// <param name="Agent">The catalog entry to run.</param>
/// <param name="Mode">Open, attach or resume (the engine always passes Open).</param>
/// <param name="Attended">True when a person opened it (the viewer shows it); false for the engine's agent runtime (no viewer, tmux, approved argv only).</param>
/// <param name="RepoPath">The repo checkout the session starts in.</param>
/// <param name="Instance">The workflow instance an unattended launch belongs to, or null.</param>
/// <param name="Worktree">The engine-computed worktree '&lt;repo&gt;/.claude/worktrees/sindri-&lt;instance&gt;-&lt;run&gt;' (created by the launcher when missing), or null.</param>
/// <param name="Step">The Step's arguments; non-null only when <paramref name="Attended"/> is false.</param>
/// <param name="Run">The Step run's token, or null.</param>
/// <param name="Resume">For a Resume: the session id to resume (the item's newest resumable launch), else null.</param>
/// <param name="Evaluator">Non-null for the holdout evaluator (LN-LCH2): the launcher runs it in its own scratch checkout instead of <paramref name="Worktree"/>.</param>
public sealed record LaunchRequest(
    LaunchId Launch,
    ItemId Item,
    AgentId Agent,
    LaunchMode Mode,
    bool Attended,
    string RepoPath,
    InstanceAddress? Instance,
    string? Worktree,
    StepArgs? Step,
    RunToken? Run,
    SessionId? Resume = null,
    EvaluatorLaunch? Evaluator = null)
{
    /// <summary>
    /// The commit the run's worktree started from (sandbox spec §3.8): the host-side prepare reads the lockfiles at it, never from the worktree. The engine sets it for a phase workspace (the phase seed);
    /// for any other Step the launcher records the repo's HEAD at the run's first launch and passes the same commit on every re-launch. Null when none is known, and the prepare is then skipped.
    /// </summary>
    public string? BaseCommit { get; init; }

    /// <summary>Null, or the skill block this Step runs (skill flows spec §3.3): its seed is the skill's invocation alone, and its session gets AW_RESULT, AW_RESULT_SKILL, AW_RESULT_FILE and AW_DIR. Never serialized.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Skills.SkillLaunch? Skill { get; init; }

    /// <summary>Null, or the state's timeout, which the sandbox plan uses as the Step's wall limit instead of the 4 h default (skill flows spec D8). Never serialized.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public TimeSpan? StepTimeout { get; init; }

    /// <summary>True when the owner's approval for this repo covers the Step's <see cref="StepPolicy.Github"/> ask (skill flows spec D7). Nothing sets it yet. Never serialized.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool GithubPushApproved { get; init; }
}
