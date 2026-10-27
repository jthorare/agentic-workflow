using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>The closed 'reason' sets of the sandbox launch codes (sandbox spec §6). A reason rides on the error as its 'reason' extension.</summary>
public static partial class SandboxReasons
{
    /// <summary>-014: no bubblewrap.</summary>
    public const string NoBwrap = "no-bwrap";

    /// <summary>-014: the system blocks unprivileged user namespaces.</summary>
    public const string NoUserNamespace = "no-user-namespace";

    /// <summary>-014: no systemd user manager.</summary>
    public const string NoUserManager = "no-user-manager";

    /// <summary>-014: the account has no memory or CPU controllers.</summary>
    public const string NoCgroupControllers = "no-cgroup-controllers";

    /// <summary>-014: no socat for the forwarders.</summary>
    public const string NoForwarder = "no-forwarder";

    /// <summary>-014: macOS (until SB-9) and other platforms.</summary>
    public const string UnsupportedPlatform = "unsupported-platform";

    /// <summary>-015: the entry's policy form is 'none'.</summary>
    public const string FormNone = "form-none";

    /// <summary>-015: the entry's policy form is reserved (Cursor).</summary>
    public const string FormReserved = "form-reserved";

    /// <summary>-015: the agent can't restrict a tool the Step allows.</summary>
    public const string ToolNotExpressible = "tool-not-expressible";

    /// <summary>-015: the agent's binary lives in a denied tree.</summary>
    public const string BinDenied = "bin-denied";

    /// <summary>-015: the host isn't tmux.</summary>
    public const string HostNotTmux = "host-not-tmux";

    /// <summary>-015: the worktree isn't under the repo.</summary>
    public const string WorktreeOutsideRepo = "worktree-outside-repo";

    /// <summary>-015: a resume found no stored plan.</summary>
    public const string NoStoredPlan = "no-stored-plan";

    /// <summary>-016: the 'fs-deny' canary.</summary>
    public const string FsDeny = "fs-deny";

    /// <summary>-016: the 'fs-write' canary.</summary>
    public const string FsWrite = "fs-write";

    /// <summary>-016: the 'git-ref' canary.</summary>
    public const string GitRef = "git-ref";

    /// <summary>-016: the 'env' canary.</summary>
    public const string Env = "env";

    /// <summary>-016: the 'login' canary.</summary>
    public const string Login = "login";

    /// <summary>-016: the 'hooks-run' canary.</summary>
    public const string HooksRun = "hooks-run";

    /// <summary>-016: the 'net-forward' canary.</summary>
    public const string NetForward = "net-forward";

    /// <summary>-016: the 'net-deny' canary.</summary>
    public const string NetDeny = "net-deny";

    /// <summary>-017: the source has no login for the target.</summary>
    public const string NoLogin = "no-login";

    /// <summary>-017: the login expired.</summary>
    public const string Expired = "expired";

    /// <summary>-017: the target has no adapter for the chosen source.</summary>
    public const string SourceUnsupported = "source-unsupported";

    /// <summary>-018: the agent GitHub App key is missing.</summary>
    public const string NoKey = "no-key";

    /// <summary>-018: the template of a GitHub refusal; the real reason is 'http-' and the three-digit status.</summary>
    public const string HttpStatus = "http-<status>";

    /// <summary>-018: no network to GitHub.</summary>
    public const string NoNetwork = "no-network";

    /// <summary>-019 (and kind of a limit kill): memory.</summary>
    public const string Memory = "memory";

    /// <summary>-019: wall time.</summary>
    public const string Wall = "wall";

    /// <summary>-020: the session cap.</summary>
    public const string Sessions = "sessions";

    /// <summary>-021: the install failed.</summary>
    public const string InstallFailed = "install-failed";

    /// <summary>-021: a declared directory has no lockfile at the base commit.</summary>
    public const string LockfileMissing = "lockfile-missing";

    /// <summary>-021: the prepare's own limit.</summary>
    public const string Timeout = "timeout";

    /// <summary>Every (code, reason) pair the spec defines; the copy goldens cover exactly these.</summary>
    public static IReadOnlyList<(string Code, string Reason)> All { get; } =
    [
        (SindriErrors.LaunchSandboxUnavailable.Code, NoBwrap), (SindriErrors.LaunchSandboxUnavailable.Code, NoUserNamespace), (SindriErrors.LaunchSandboxUnavailable.Code, NoUserManager),
        (SindriErrors.LaunchSandboxUnavailable.Code, NoCgroupControllers), (SindriErrors.LaunchSandboxUnavailable.Code, NoForwarder), (SindriErrors.LaunchSandboxUnavailable.Code, UnsupportedPlatform),
        (SindriErrors.LaunchUnconfinable.Code, FormNone), (SindriErrors.LaunchUnconfinable.Code, FormReserved), (SindriErrors.LaunchUnconfinable.Code, ToolNotExpressible), (SindriErrors.LaunchUnconfinable.Code, BinDenied),
        (SindriErrors.LaunchUnconfinable.Code, HostNotTmux), (SindriErrors.LaunchUnconfinable.Code, WorktreeOutsideRepo), (SindriErrors.LaunchUnconfinable.Code, NoStoredPlan),
        (SindriErrors.LaunchSelfCheckFailed.Code, FsDeny), (SindriErrors.LaunchSelfCheckFailed.Code, FsWrite), (SindriErrors.LaunchSelfCheckFailed.Code, GitRef), (SindriErrors.LaunchSelfCheckFailed.Code, Env), (SindriErrors.LaunchSelfCheckFailed.Code, Login),
        (SindriErrors.LaunchSelfCheckFailed.Code, HooksRun), (SindriErrors.LaunchSelfCheckFailed.Code, NetForward), (SindriErrors.LaunchSelfCheckFailed.Code, NetDeny),
        (SindriErrors.LaunchLoginUnavailable.Code, NoLogin), (SindriErrors.LaunchLoginUnavailable.Code, Expired), (SindriErrors.LaunchLoginUnavailable.Code, SourceUnsupported),
        (SindriErrors.LaunchGitHubTokenFailed.Code, NoKey), (SindriErrors.LaunchGitHubTokenFailed.Code, HttpStatus), (SindriErrors.LaunchGitHubTokenFailed.Code, NoNetwork),
        (SindriErrors.LaunchLimitHit.Code, Memory), (SindriErrors.LaunchLimitHit.Code, Wall),
        (SindriErrors.LaunchSessionWait.Code, Sessions),
        (SindriErrors.LaunchPrepareFailed.Code, InstallFailed), (SindriErrors.LaunchPrepareFailed.Code, LockfileMissing), (SindriErrors.LaunchPrepareFailed.Code, Timeout),
    ];

    /// <summary>Whether <paramref name="reason"/> is one of <paramref name="code"/>'s reasons ('http-' and a three-digit status counts for -018).</summary>
    /// <param name="code">The launch code.</param>
    /// <param name="reason">The reason.</param>
    /// <returns>True when the pair is defined.</returns>
    public static bool IsDefined(string code, string reason) =>
        (!string.Equals(reason, HttpStatus, StringComparison.Ordinal) && All.Any(p => string.Equals(p.Code, code, StringComparison.Ordinal) && string.Equals(p.Reason, reason, StringComparison.Ordinal)))
        || (string.Equals(code, SindriErrors.LaunchGitHubTokenFailed.Code, StringComparison.Ordinal) && HttpReason.IsMatch(reason));

    [GeneratedRegex("^http-[0-9]{3}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex HttpReason { get; }
}
