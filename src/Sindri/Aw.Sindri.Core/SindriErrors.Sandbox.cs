using Aw.Kernel;

namespace Aw.Sindri.Core;

// The sandbox launch codes (sandbox spec §6, -014 to -021); the session id pattern timeout in SindriErrors.cs is -030, since the sandbox reserved -015 first.
public static partial class SindriErrors
{
    /// <summary>SND-LAUNCH-014: the sandbox isn't available on this box (sandbox spec §6; kind no-runtime); detail 'reason' is no-bwrap, no-user-namespace, no-user-manager, no-cgroup-controllers, no-forwarder or unsupported-platform.</summary>
    public static readonly ErrorDefinition LaunchSandboxUnavailable = new("SND-LAUNCH-014", ErrorKind.Unavailable, "The sandbox isn't available on this machine");

    /// <summary>SND-LAUNCH-015: the target or its form can't be confined to this policy (kind launch-park; Resume starts a new run).</summary>
    public static readonly ErrorDefinition LaunchUnconfinable = new("SND-LAUNCH-015", ErrorKind.Conflict, "The agent can't be confined to this Step's policy");

    /// <summary>SND-LAUNCH-016: the sandbox self-check failed; 'reason' names the canary (kind launch-park).</summary>
    public static readonly ErrorDefinition LaunchSelfCheckFailed = new("SND-LAUNCH-016", ErrorKind.Conflict, "The sandbox self-check failed");

    /// <summary>SND-LAUNCH-017: the target's model login isn't available from the chosen credential source (kind no-runtime); 'reason' is no-login, expired or source-unsupported, with the 'source'.</summary>
    public static readonly ErrorDefinition LaunchLoginUnavailable = new("SND-LAUNCH-017", ErrorKind.Conflict, "The agent's sign-in isn't available for Steps");

    /// <summary>SND-LAUNCH-018: the agent GitHub App token couldn't be minted (kind step-failure; 'agent-app' only).</summary>
    public static readonly ErrorDefinition LaunchGitHubTokenFailed = new("SND-LAUNCH-018", ErrorKind.Unavailable, "The agent GitHub token couldn't be minted");

    /// <summary>SND-LAUNCH-019: a sandbox limit ended the session (kind step-failure). Catalogue only: it rides on session.ended and step.failed and is never an HTTP response.</summary>
    public static readonly ErrorDefinition LaunchLimitHit = new("SND-LAUNCH-019", ErrorKind.Unavailable, "A sandbox limit ended the session");

    /// <summary>SND-LAUNCH-020: the cap on concurrent sandboxed sessions is reached (kind resource-wait: the launch activity retries, an attended open is refused).</summary>
    public static readonly ErrorDefinition LaunchSessionWait = new("SND-LAUNCH-020", ErrorKind.Unavailable, "No free session slot");

    /// <summary>SND-LAUNCH-021: the host-side prepare of the repo's packages failed (kind step-failure); 'reason' is install-failed, lockfile-missing or timeout.</summary>
    public static readonly ErrorDefinition LaunchPrepareFailed = new("SND-LAUNCH-021", ErrorKind.Unavailable, "The repo's packages couldn't be prepared for the Step");
}
