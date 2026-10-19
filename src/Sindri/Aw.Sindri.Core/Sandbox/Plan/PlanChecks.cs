using System.Text;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Credentials;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>
/// The checks that decide whether a request can be planned at all: the policy and the paths (-015), the socket path (-016) and the
/// prepared login (-016). Nothing is bound from a string these checks have not seen: an empty or unnormalized path must not widen a bind.
/// </summary>
internal static partial class PlanChecks
{
    private const int SocketPathLimit = 104;
    private const int MaxProjectKey = 200;

    private static readonly string[] _renameWritten = [".credentials.json", ".claude.json", "auth.json"];

    internal static Error? Validate(PlanContext c) => Mode(c) ?? Paths(c) ?? Namespace(c) ?? Worktree(c) ?? TranscriptKey(c) ?? Socket(c) ?? HomeEntries(c) ?? Login(c);

    private static Error? Mode(PlanContext c) =>
        c.Request.Policy is { Mode: not StepPermissionMode.Default, WritablePath: not null }
            ? Unconfinable("a Step in a read-only or plan mode names a writable path")
            : null;

    private static Error? Paths(PlanContext c)
    {
        var r = c.Request;
        var p = r.Paths;
        (string? Path, string What)[] clean =
        [
            (p.Repo, "the repo path"), (p.GitCommonDir, "the git directory"), (p.RunDirectory, "the run directory"), (p.RunArea, "the run area"),
            (p.Worktree ?? p.Repo, "the worktree"), (r.Host.Home, "the home"), (r.Host.StateDir, "the state directory"), (r.Host.SindriHome, "the Sindri home"), (r.Net.NetDir, "the socket directory"),
        ];
        var bad = clean.FirstOrDefault(x => !IsClean(x.Path));
        if (bad.What is not null)
        {
            return Unconfinable($"{bad.What} is not an absolute normalized path");
        }

        if (!IsGitDirectory(p.GitCommonDir, p.Repo))
        {
            return Unconfinable("the git directory is not the repo's .git or inside it");
        }

        if (p.Worktree is { } worktree && !Inside(worktree, p.Repo))
        {
            return Unconfinable("the worktree is not inside the repo");
        }

        return !Inside(p.RunDirectory, r.Host.SindriHome) || !Inside(p.RunArea, r.Host.SindriHome)
            ? Unconfinable("the run directory and the run area belong inside the Sindri home")
            : null;
    }

    private static Error? Namespace(PlanContext c)
    {
        var ns = c.Request.RunNamespace;
        return !RunNamespace.IsMatch(ns) || ns.Contains("..", StringComparison.Ordinal) || ns.Split('/').Any(s => s.EndsWith(".lock", StringComparison.Ordinal))
            ? Unconfinable("the run namespace is not sindri/<instance>/<run>")
            : null;
    }

    private static Error? Worktree(PlanContext c)
    {
        var paths = c.Request.Paths;
        return c.WritablePath is { } writable && !string.Equals(writable, paths.Worktree, StringComparison.Ordinal)
            ? Unconfinable("the writable path is not the run's worktree")
            : null;
    }

    // Claude Code shortens a project key past 200 characters and appends a hash this build cannot compute: the transcript would land in the throw-away config directory and be lost with it.
    private static Error? TranscriptKey(PlanContext c) =>
        c.Worktree is { } worktree && c.Request.Login is not null && ProjectKey.For(worktree).Length > MaxProjectKey
            ? Unconfinable("the worktree path is too long for Claude Code's transcript directory name")
            : null;

    private static Error? Socket(PlanContext c) =>
        Encoding.UTF8.GetByteCount(c.Request.Net.NetDir + "/sindri.sock") >= SocketPathLimit
            ? SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.NetForward, "the socket directory is too long for a unix socket path")
            : null;

    // The agent-home write set holds for the profile's home entries too: a read-write entry may not open the owner's hooks or settings.
    private static Error? HomeEntries(PlanContext c)
    {
        IEnumerable<string> writable =
        [
            .. (c.Request.Settings?.Fs?.HomeAllow ?? []).Concat(c.Repo.HomeAllow ?? []).Where(e => e.Mode == MountMode.ReadWrite && e.Path is not null).Select(e => e.Path!),   // why: the profile rules require a path
            .. c.Repo.CachePaths ?? [],
        ];
        return writable.FirstOrDefault(p => OutsideWriteSet(c.Home, $"{c.Home}/{p}")) is { } path
            ? Failed(SandboxReasons.Login, $"'{path}' opens the agent home read-write outside its named write set")
            : null;
    }

    private static Error? Login(PlanContext c)
    {
        if (c.Request.Login is not { } login)
        {
            return null;
        }

        if (login.Source != c.Login)
        {
            return Failed(SandboxReasons.Login, "the prepared login is not from the source the plan resolved");
        }

        return LoginMount(c, login) ?? LoginEnvironment(login);
    }

    private static Error? LoginMount(PlanContext c, PreparedLogin login)
    {
        foreach (var mount in login.Mounts)
        {
            if (_renameWritten.Any(f => mount.SandboxPath.EndsWith("/" + f, StringComparison.Ordinal)))
            {
                return Failed(SandboxReasons.Login, "a login file is rewritten by rename: bind its directory, not the file");
            }

            if (mount.Mode == MountMode.ReadWrite && OutsideWriteSet(c.Home, mount.SandboxPath))
            {
                return Failed(SandboxReasons.Login, "the agent home is read-only except its named write set");
            }
        }

        return null;
    }

    private static bool OutsideWriteSet(string home, string path)
    {
        foreach (var agentHome in new[] { home + "/.claude", home + "/.codex" })
        {
            if (string.Equals(path, agentHome, StringComparison.Ordinal))
            {
                return true;
            }

            if (path.StartsWith(agentHome + "/", StringComparison.Ordinal))
            {
                return !AgentHomeWriteSet.Allows(agentHome, path[(agentHome.Length + 1)..]);
            }
        }

        return false;
    }

    private static Error? LoginEnvironment(PreparedLogin login)
    {
        if (login.Environment.Keys.FirstOrDefault(n => !EnvironmentAllowlist.IsCredentialName(n)) is { } name)
        {
            return Failed(SandboxReasons.Env, $"the login sets {name}: a credential source may set only the agent home and the model key");
        }

        return login.Environment.TryGetValue("CODEX_HOME", out var codex) && (string.Equals(codex, "/tmp", StringComparison.Ordinal) || codex.StartsWith("/tmp/", StringComparison.Ordinal))
            ? Failed(SandboxReasons.Login, "Codex will not make its helper binaries under /tmp")
            : null;
    }

    private static bool IsClean(string? path) =>
        !string.IsNullOrEmpty(path) && path[0] == '/' && path.Length > 1 && !path.EndsWith('/') && !path.Contains("//", StringComparison.Ordinal)
        && !path.Split('/').Any(s => string.Equals(s, ".", StringComparison.Ordinal) || string.Equals(s, "..", StringComparison.Ordinal));

    // Spec SB5: the common git directory is <repo>/.git, or under it (a submodule's modules/<name>).
    private static bool IsGitDirectory(string git, string repo) => string.Equals(git, repo + "/.git", StringComparison.Ordinal) || Inside(git, repo + "/.git");

    private static bool Inside(string path, string root) => path.StartsWith(root + "/", StringComparison.Ordinal);

    private static Error Unconfinable(string detail) => SandboxRefusal.For(SindriErrors.LaunchUnconfinable, SandboxReasons.WorktreeOutsideRepo, detail);

    private static Error Failed(string reason, string detail) => SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, reason, detail);

    [GeneratedRegex("^sindri(/[A-Za-z0-9_-]([A-Za-z0-9._-]*[A-Za-z0-9_-])?){2}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex RunNamespace { get; }
}
