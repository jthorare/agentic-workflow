using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The pure half of an agent Step's launch (engine P8 D4, D8): its deterministic launch id and the unattended request the engine hands to the launcher.</summary>
public static class AgentLaunches
{
    private static readonly string[] _report = ["Bash(sindri workflow complete:*)", "Bash(sindri workflow status:*)", "Bash(sindri workflow events:*)"];
    private static readonly string[] _evidence = ["Bash(ui-evidence:*)"];
    private static readonly string[] _commit = ["Bash(git add:*)", "Bash(git commit:*)", "Bash(git status:*)", "Bash(git diff:*)"];

    /// <summary>The launch id of a run: 'LCH-' and the first 32 hex digits of SHA-256 of the instance id, ':' and the run token. A retry or a replay can never start a second session.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The id.</returns>
    public static LaunchId LaunchIdFor(AgentStepRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return LaunchIdFor(run.Workflow, run.Run, run.Escalation);
    }

    /// <summary>The launch id of a run, from the instance and the run token alone: what a status read names before the launch activity has run.</summary>
    /// <param name="workflow">The instance.</param>
    /// <param name="run">The run token.</param>
    /// <returns>The id.</returns>
    /// <param name="escalation">0 for the first launch of the run, n for its n-th retry on a stronger model; 0 keeps every id that exists today.</param>
    public static LaunchId LaunchIdFor(WorkflowInstanceId workflow, RunToken run, int escalation = 0)
    {
        var escalated = escalation > 0 ? string.Create(CultureInfo.InvariantCulture, $":escalation:{escalation}") : string.Empty;   // why: n = 0 keeps every id that exists today
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(workflow.Value + ":" + run.Value + escalated));
        return LaunchId.From("LCH-" + Convert.ToHexStringLower(digest)[..32]);
    }

    /// <summary>The worktree the run works in: the workspace the Step reads when it names one ('fact:workspace', an existing worktree the launcher then uses as it is), else the one the launcher creates, '&lt;repo&gt;/.claude/worktrees/sindri-&lt;instance&gt;-&lt;run&gt;' ('-e1' after it for the retry on a stronger model).</summary>
    /// <param name="run">The run.</param>
    /// <param name="target">Where it runs.</param>
    /// <param name="workspace">The workspace the Step reads; null when it reads none.</param>
    /// <returns>The path.</returns>
    public static string WorktreeFor(AgentStepRun run, AgentStepTarget target, string? workspace = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(target);
        return !string.IsNullOrWhiteSpace(workspace) ? workspace : target.RepoPath.TrimEnd('/') + "/.claude/worktrees/sindri-" + run.Workflow.Value + "-" + run.Run.Value + (run.Escalation > 0 ? string.Create(CultureInfo.InvariantCulture, $"-e{run.Escalation}") : string.Empty);   // why: the retry works in a fresh worktree, not the failed run's
    }

    /// <summary>The launcher's request: unattended, Open, the worktree, the Step's reference and the policy its capability gives.</summary>
    /// <param name="run">The run.</param>
    /// <param name="target">Where it runs.</param>
    /// <param name="descriptor">The Step's descriptor, for the brief; null when the catalog doesn't know it.</param>
    /// <param name="extras">What the Step is told, the workspace it reads and the pack it bundles; null for none.</param>
    /// <returns>The request.</returns>
    public static LaunchRequest RequestFor(AgentStepRun run, AgentStepTarget target, BlockDescriptor? descriptor = null, AgentLaunchExtras? extras = null)
    {
        var worktree = WorktreeFor(run, target, extras?.Workspace);
        // Never plan mode: plan mode runs read-only tools only, so the session couldn't run 'sindri workflow complete'. A Step only tightens the
        // profile: every Step may report, a write Step may write its own worktree and commit it, and nothing else is allowed (no build tools: SC-R).
        string[] tools = [.. _report, .. EvidenceGrants.For(run.Step.Block), .. run.WriteScope ? _commit : []];   // an evidence Step also gets its own verbs, one grant each (cutover spec §3.1)
        var policy = new StepPolicy(tools, StepPermissionMode.Default, run.WriteScope ? worktree : null) { TurnMarkers = extras?.Skill is not null, Github = extras?.Skill is { Block.Pushes: true, Trial: false } ? GithubCredentials.AgentApp : null };   // a skill session's prompt hook marks its turns (the result read needs them)
        return new LaunchRequest(
            LaunchIdFor(run), target.Item, target.Agent, LaunchMode.Open, Attended: false, target.RepoPath, run.Address, worktree,
            StepArgsFor(run, descriptor, extras, policy, LaunchIdFor(run)), run.Run)
        {
            BaseCommit = extras?.BaseCommit,
            Skill = extras?.Skill,
            StepTimeout = extras?.StepTimeout,
        };
    }

    // The Step's 'model' parameter (null runs the agent's default), its brief and the pack and facts it is given.
    private static StepArgs StepArgsFor(AgentStepRun run, BlockDescriptor? descriptor, AgentLaunchExtras? extras, StepPolicy policy, LaunchId launch) =>
        extras?.Skill is { } skill ? SkillStepArgs(run, extras, skill, policy, launch) : new(Model: extras?.Model, policy, run.Step, AgentStepBrief.Render(run, descriptor, extras?.Instructions, extras?.Facts?.Keys), extras?.Bundle, extras?.Facts is { Count: > 0 } facts ? facts : null);

    // A skill block's seed is the skill's invocation alone (skill flows spec D2), not the Step brief that tells the agent to call 'sindri workflow complete'.
    // Every input also goes into the run directory as '<key>.json', which is where a long or risky text's seed points.
    private static StepArgs SkillStepArgs(AgentStepRun run, AgentLaunchExtras extras, SkillLaunch skill, StepPolicy policy, LaunchId launch)
    {
        Dictionary<string, string> files = new(StringComparer.Ordinal);
        foreach (var (name, content) in extras.Facts ?? Enumerable.Empty<KeyValuePair<string, string>>())
        {
            files[name] = content;
        }

        foreach (var (key, value) in skill.Inputs)
        {
            files[key + ".json"] = JsonValue.Create(value).ToJsonString();
        }

        return new(Model: extras.Model, policy, run.Step, SkillSeed.Render(skill, SkillRunDirectory(run, extras.SindriHome, launch)), extras.Bundle, files);
    }

    // why: the run directory is the launcher's ('$AW_SINDRI_RUN_DIR', StepEnvironment.RunDirectoryOf); without Sindri's home (a test) the seed names the variable.
    private static string SkillRunDirectory(AgentStepRun run, string? sindriHome, LaunchId launch) =>
        sindriHome is null ? "$" + StepEnvironment.RunDirectory : StepEnvironment.RunDirectoryAt(run.Workflow, run.Step.State, launch, sindriHome);

    /// <summary>The holdout evaluator's request (LN-LCH2): it runs in a scratch checkout at the head sha, reads the holdout set and never writes it, can't touch the phase worktree, and may run the evidence CLIs.</summary>
    /// <param name="run">The run.</param>
    /// <param name="target">Where it runs.</param>
    /// <param name="evaluator">The head sha, the holdout set and the phase worktree to keep out of reach.</param>
    /// <param name="descriptor">The Step's descriptor, for the brief; null when the catalog doesn't know it.</param>
    /// <param name="extras">What the Step is told to do (its instructions, for the brief) and the model it runs on (the pick); null for none and the agent's default.</param>
    /// <returns>The request.</returns>
    public static LaunchRequest EvaluatorRequestFor(AgentStepRun run, AgentStepTarget target, EvaluatorLaunch evaluator, BlockDescriptor? descriptor = null, AgentLaunchExtras? extras = null)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        // The launcher fills in the writable path: the scratch checkout it makes. No commit rights: the evaluator reports, it doesn't change the repo.
        var policy = new StepPolicy([.. _report, .. _evidence], StepPermissionMode.Default, WritablePath: null, ReadOnlyPaths: [evaluator.HoldoutPath], DeniedPaths: [evaluator.PhaseWorktree]);
        return new LaunchRequest(
            LaunchIdFor(run), target.Item, target.Agent, LaunchMode.Open, Attended: false, target.RepoPath, run.Address, Worktree: null,
            new StepArgs(Model: extras?.Model, policy, run.Step, AgentStepBrief.Render(run, descriptor, extras?.Instructions)), run.Run, Evaluator: evaluator);
    }
}
