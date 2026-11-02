using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>The box- and Host-specific facts <see cref="SandboxLaunchStages"/> cannot get from the launch or the profile (the switch step fills them from the real box).</summary>
internal interface ISandboxLaunchInputs
{
    /// <summary>The host facts the plan builder reads: home, state dir, real Sindri home, installed binaries, uid, RAM, cores, environment allowlist source, other homes, hook inventory.</summary>
    /// <returns>The facts.</returns>
    public SandboxHostFacts Host();

    /// <summary>The repo's common git directory (<c lang="text">git rev-parse --git-common-dir</c>, absolute).</summary>
    /// <param name="repoPath">The repo checkout.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The path.</returns>
    public ValueTask<string> GitCommonDir(string repoPath, CancellationToken cancellationToken);

    /// <summary>The launcher's per-run area for the launch (scratch, shims, hook spool, plan file, wrap limits).</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>The path.</returns>
    public string RunAreaFor(LaunchId launch);

    /// <summary>The Step's run directory the launcher makes (<see cref="StepEnvironment.RunDirectoryOf"/> over Sindri's own home), or null for a launch with none.</summary>
    /// <param name="request">The launch.</param>
    /// <returns>The path, or null.</returns>
    public string? RunDirectoryFor(LaunchRequest request);

    /// <summary>The short per-launch socket directory on the host.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>The path.</returns>
    public string NetDirFor(LaunchId launch);

    /// <summary>The control plane's port (what the inside forwarder listens on and the host forwarder dials).</summary>
    /// <returns>The port.</returns>
    public int SindriPort();

    /// <summary>The port the plan tells the agent's proxy variables to use inside the sandbox.</summary>
    /// <returns>The port.</returns>
    public int ProxyPort();

    /// <summary>The launcher's directory of Step policy files (<c lang="text">LauncherOptions.PolicyDirectory</c>).</summary>
    /// <returns>The path.</returns>
    public string PolicyDirectory();

    /// <summary>How the agent takes its Step policy (a settings file, '-c' overrides, none).</summary>
    /// <param name="agent">The agent.</param>
    /// <returns>The form from the catalog, or none for an agent it does not have.</returns>
    public PolicyForm PolicyFormOf(AgentId agent);

    /// <summary>The launch's denial counter (its file, scrubber and clock), which the egress counts into and the end of the launch flushes.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>The counter, or null to count nothing.</returns>
    public DenialCounter? DenialCounterFor(LaunchId launch);

    /// <summary>The catalog entry's transcript glob (with '{sessionId}').</summary>
    /// <param name="agent">The agent.</param>
    /// <returns>The glob, or null when the entry has none.</returns>
    public string? TranscriptGlob(AgentId agent);

    /// <summary>The session id the launch's agent reported (the file 'session-id-report' writes under the real Sindri home's launches folder).</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>The id, or null when none was reported.</returns>
    public string? ReportedSession(LaunchId launch);
}
