using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>
/// The parts of a launch's sandbox step that other SB-5a pieces write (the session home, the policy files, the egress registry, the denial collector): <see cref="SandboxLaunch"/>
/// calls them in this order and owns only the order, the cap, the scope wrap and the cleanup after a failure.
/// </summary>
internal interface ISandboxLaunchStages
{
    /// <summary>Builds the request from the launch: the repo's profile settings, host paths, net facts, learn history, and the prepared login (credentials).</summary>
    /// <param name="request">The launch.</param>
    /// <param name="chosenSession">The session id the launcher chose for the agent, or null; kept for the denial collector.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The request, or the SND-LAUNCH-015/017 refusal.</returns>
    public ValueTask<Result<SandboxRequest>> Request(LaunchRequest request, SessionId? chosenSession, CancellationToken cancellationToken);

    /// <summary>Writes the Step's policy file into the session home with the plan's rules merged in (a claude-settings-file Step; the sandbox can't see the launcher's own directory), and gives the extra arguments the Step's form takes under a sandbox (a Codex Step's '-c' overrides).</summary>
    /// <param name="request">The request the plan was built from.</param>
    /// <param name="plan">The plan.</param>
    /// <param name="sessionHome">The launch's session home.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The extra arguments and the file's move, or a refusal.</returns>
    public ValueTask<Result<PolicyFiles>> WritePolicy(SandboxRequest request, SandboxPlan plan, string sessionHome, CancellationToken cancellationToken);

    /// <summary>Starts the launch's egress (registered per launch in the LaunchSandboxRegistry) before the self-check.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Success, or the SND-LAUNCH-016 refusal.</returns>
    public ValueTask<Result<Unit>> StartEgress(SandboxPlan plan, CancellationToken cancellationToken);

    /// <summary>What the end of a launch releases besides the session home (the adapter removes that): its denials are collected, then its egress stops. A launch with none is a no-op.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes when it is gone.</returns>
    public ValueTask Release(LaunchId launch, CancellationToken cancellationToken);
}
