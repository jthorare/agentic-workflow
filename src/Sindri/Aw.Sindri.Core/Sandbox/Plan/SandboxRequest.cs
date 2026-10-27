using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>Everything the pure plan builder needs for one launch (sandbox spec SB12).</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Item">The item.</param>
/// <param name="Target">The agent.</param>
/// <param name="Repo">The repo name (key of 'sandbox.repos').</param>
/// <param name="Instance">The workflow instance, or null for an attended session.</param>
/// <param name="Paths">The launch's paths.</param>
/// <param name="RunNamespace">The run's ref namespace 'sindri/&lt;instance&gt;/&lt;run&gt;' (SB5).</param>
/// <param name="Policy">The engine's Step policy; null for an attended session, which gets the fixed attended policy (§3.9).</param>
/// <param name="Attended">True for a session the owner opened with 'sindri open'.</param>
/// <param name="StepTimeout">The Step's timeout, or null.</param>
/// <param name="LongMax">'workflow.steps.longMax', or null.</param>
/// <param name="SlotHeld">True when the Step already holds a heavy slot (LN-WF3).</param>
/// <param name="Net">The network facts.</param>
/// <param name="Host">The host facts.</param>
/// <param name="Settings">The approved profile's sandbox section; null means the built-in defaults.</param>
/// <param name="Login">The prepared login, or null for a plan built only to check the form.</param>
/// <param name="OmitGroups">Groups the Step removes from itself (a Step only tightens).</param>
/// <param name="ExtraDeny">More denied paths from the line's capability policy (holdout store, other runs' worktrees).</param>
public sealed record SandboxRequest(
    LaunchId Launch,
    ItemId Item,
    AgentId Target,
    RepoName Repo,
    InstanceAddress? Instance,
    SandboxPaths Paths,
    string RunNamespace,
    StepPolicy? Policy,
    bool Attended,
    TimeSpan? StepTimeout,
    TimeSpan? LongMax,
    bool SlotHeld,
    SandboxNetFacts Net,
    SandboxHostFacts Host,
    SandboxSettings? Settings,
    PreparedLogin? Login,
    IReadOnlyList<string>? OmitGroups = null,
    IReadOnlyList<string>? ExtraDeny = null);
