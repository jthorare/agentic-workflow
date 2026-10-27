using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The canaries of sandbox spec §6, as sh scripts built from the plan. The network canaries that need the forwarders (net-forward, and the proxy half of net-deny) belong to SB-4a.</summary>
internal static class SandboxCanaries
{
    private const string Prelude = "fail() { echo \"$1\"; exit 1; }\n";

    // The recursive, forced delete of the root that the destructive-command hook must refuse (spec §3.1, hook canary part a).
    private const string DestructiveCommand = "rm -r" + "f /";

    // The hooks-run canary exercises the hooks the plan keeps (plan.Hooks.Kept), never a dropped one, and never rtk-rewrite: inside a Step
    // that hook grants nothing (owner, 2026-10-18, strict; SB-5a enforces it), so no canary part may rely on it.
    private static readonly string[] _preamble =
    [
        "H=\"$HOME/.claude/hooks\"",
        "S=\"${AW_STATE_DIR:-$HOME/.agentic-workflow}\"",
    ];

    private static readonly string[] _blockDestructive =
    [
        "printf '%s' '{\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"" + DestructiveCommand + "\"}}' | \"$H/block-destructive.sh\" >/dev/null 2>&1; rc=$?",
        "[ \"$rc\" -eq 2 ] || fail \"block-destructive let a recursive delete of the root through (exit $rc)\"",
    ];

    private static readonly string[] _detectSecrets =
    [
        "printf '%s' '{\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"echo AKIAIOSFODNN7EXAMPLE\"}}' | \"$H/detect-secrets.sh\" >/dev/null 2>&1; rc=$?",
        "[ \"$rc\" -eq 2 ] || fail \"detect-secrets let a planted key through (exit $rc)\"",
    ];

    private static readonly string[] _judgeAndState =
    [
        "out=$(judge brief get task:selfcheck 2>&1); rc=$?",
        "{ [ \"$rc\" -eq 1 ] && printf '%s' \"$out\" | grep -q 'no brief'; } || fail \"the judge round trip failed (exit $rc): $out\"",
        "[ -d \"$S/judge\" ] && [ -d \"$S/locks\" ] || fail \"the shared judge or locks directory is not bound in\"",
        "mkdir -p \"$S/judge/sessions\" && : > \"$S/judge/sessions/.sindri-selfcheck-$$\" && rm -f \"$S/judge/sessions/.sindri-selfcheck-$$\" || fail \"judge/sessions is not writable\"",
        ". \"$H/lib/failopen.sh\" || fail \"lib/failopen.sh is missing\"",
        "aw_failopen sindri-selfcheck canary",
        "grep -q sindri-selfcheck \"$S/logs/failopen.log\" 2>/dev/null || fail \"the fail-open line did not reach the per-launch log\"",
        "rm -f \"$S/logs/failopen.log\"",
    ];

    private static readonly string[] _metaOutbox =
    [
        "printf '%s' '{\"tool_name\":\"SendMessage\",\"tool_input\":{\"message\":\"x\"}}' | \"$H/meta-outbox.sh\" >/dev/null 2>&1; rc=$?",
        "[ \"$rc\" -eq 0 ] || fail \"meta-outbox exited $rc\"",
    ];

    private static readonly string[] _closing =
    [
        "[ ! -e \"$S/bin/aw-meta\" ] || fail \"aw-meta is visible in the session\"",
        "exit 0",
    ];

    private static string HookScript(SandboxPlan plan)
    {
        var kept = plan.Hooks.Kept;
        var lines = new List<string>(_preamble);
        if (kept.Contains("block-destructive", StringComparer.Ordinal))
        {
            lines.AddRange(_blockDestructive);
        }

        if (kept.Contains("detect-secrets", StringComparer.Ordinal))
        {
            lines.AddRange(_detectSecrets);
        }

        lines.AddRange(_judgeAndState);
        if (kept.Contains("meta-outbox", StringComparer.Ordinal))
        {
            lines.AddRange(_metaOutbox);
        }

        lines.AddRange(_closing);
        lines.Add(string.Empty);
        return Prelude + string.Join('\n', lines);
    }

    /// <summary>Builds the canaries a plan can be proven with, in the order they run.</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>The canaries.</returns>
    internal static IReadOnlyList<SandboxCanary> For(SandboxPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        List<SandboxCanary> canaries = [FsDeny(plan), FsWrite(plan), Env(), Login(plan), new SandboxCanary(SandboxReasons.HooksRun, HookScript(plan), Shell: "/bin/bash"), NetDeny()];
        if (plan.Worktree is { } worktree && plan.Writable.Contains(worktree, StringComparer.Ordinal))
        {
            canaries.Insert(2, GitRef(plan, worktree));
        }

        return canaries;
    }

    /// <summary>Quotes a value for sh: single quotes, with each embedded single quote closed, escaped and reopened.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The quoted text.</returns>
    internal static string Quote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    // A denied path is absent, or an empty mask; a directory shows only what the plan binds back into it (the hook inventory inside the state directory).
    private static SandboxCanary FsDeny(SandboxPlan plan)
    {
        var script = new StringBuilder(Prelude);
        script.Append("check() { p=$1; shift; if [ -d \"$p\" ]; then for f in \"$p\"/* \"$p\"/.[!.]*; do [ -e \"$f\" ] || [ -L \"$f\" ] || continue; n=${f#\"$p\"/}; ok=0; for a in \"$@\"; do [ \"$n\" = \"$a\" ] && ok=1; done; [ $ok = 1 ] || fail \"visible: $f\"; done; elif [ -e \"$p\" ]; then [ ! -s \"$p\" ] || fail \"readable: $p\"; fi; }\n");
        foreach (var denied in plan.Mounts.Where(m => m.Kind == PlanMountKind.Deny))
        {
            var children = plan.Mounts
                .Where(m => m.Kind != PlanMountKind.Deny && m.Path.StartsWith(denied.Path + "/", StringComparison.Ordinal))
                .Select(m => m.Path[(denied.Path.Length + 1)..].Split('/')[0])
                .Distinct(StringComparer.Ordinal)
                .Select(Quote);
            script.Append(CultureInfo.InvariantCulture, $"check {Quote(denied.Path)} {string.Join(' ', children)}\n");
        }

        script.Append("for p in /usr /etc; do ( : > \"$p/.sindri-canary\" ) 2>/dev/null && fail \"wrote $p\"; done\nexit 0\n");
        return new SandboxCanary(SandboxReasons.FsDeny, script.ToString());
    }

    private static SandboxCanary FsWrite(SandboxPlan plan)
    {
        var script = new StringBuilder(Prelude);
        script.Append("can() { f=\"$1/.sindri-canary-$$\"; ( : > \"$f\" ) 2>/dev/null && { rm -f \"$f\"; return 0; }; return 1; }\n");
        foreach (var path in new[] { plan.RunDirectory, "/tmp" })
        {
            script.Append(CultureInfo.InvariantCulture, $"can {Quote(path)} || fail {Quote("cannot write " + path)}\n");
        }

        if (plan.Worktree is { } worktree)
        {
            var writable = plan.Writable.Contains(worktree, StringComparer.Ordinal);
            script.Append(writable ? $"can {Quote(worktree)} || fail {Quote("cannot write " + worktree)}\n" : $"can {Quote(worktree)} && fail {Quote("can write " + worktree)}\n");
        }

        script.Append("exit 0\n");
        return new SandboxCanary(SandboxReasons.FsWrite, script.ToString());
    }

    // The real worktree and its real repo: the sandbox's git settings win, an unsigned commit and two ref updates work in the run's directory, a ref elsewhere does not.
    // It never deletes a ref (a delete takes packed-refs.lock, which is read-only): the caller removes 'selfcheck' from outside.
    private static SandboxCanary GitRef(SandboxPlan plan, string worktree)
    {
        var hooks = plan.Git.First(g => string.Equals(g.Key, "core.hooksPath", StringComparison.Ordinal)).Value;
        var reference = $"refs/heads/{plan.RunNamespace}/selfcheck";
        var script = Prelude
            + $"cd {Quote(worktree)} || fail 'no worktree'\n"
            + $"[ \"$(git rev-parse --show-toplevel 2>/dev/null)\" = {Quote(worktree)} ] || fail 'the worktree is not a git work tree'\n"
            + $"[ \"$(git config --get core.hooksPath)\" = {Quote(hooks)} ] || fail \"core.hooksPath is $(git config --get core.hooksPath)\"\n"
            + "[ \"$(git config --get commit.gpgsign)\" = false ] || fail \"commit.gpgsign is $(git config --get commit.gpgsign)\"\n"
            + "tree=$(git rev-parse 'HEAD^{tree}' 2>&1) || fail \"no HEAD: $tree\"\n"
            + "c=$(git commit-tree \"$tree\" -p HEAD -m sindri-selfcheck 2>&1) || fail \"commit-tree: $c\"\n"
            + $"o=$(git update-ref {Quote(reference)} \"$c\" 2>&1) || fail \"first update-ref: $o\"\n"
            + $"o=$(git update-ref {Quote(reference)} HEAD 2>&1) || fail \"second update-ref: $o\"\n"
            + "git update-ref refs/heads/other-selfcheck HEAD >/dev/null 2>&1 && fail 'a ref outside the run directory was written'\n"
            + "exit 0\n";
        return new SandboxCanary(SandboxReasons.GitRef, script);
    }

    // The environment holds only allowlisted names, and never the heavy wrapper's held-slot marker.
    private static SandboxCanary Env() =>
        new(SandboxReasons.Env, "env\n", result =>
        {
            var names = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(l => l.Contains('=', StringComparison.Ordinal)).Select(l => l[..l.IndexOf('=', StringComparison.Ordinal)]).ToList();
            var bad = EnvironmentAllowlist.Violations(names);
            return bad.Count > 0 ? "not on the allowlist: " + string.Join(", ", bad) : names.Contains("AW_HEAVY_LOCK_HELD", StringComparer.Ordinal) ? "AW_HEAVY_LOCK_HELD is set" : null;
        });

    private static SandboxCanary Login(SandboxPlan plan)
    {
        var script = new StringBuilder(Prelude);
        var env = plan.Environment.ToDictionary(e => e.Name, e => e.Value, StringComparer.Ordinal);
        if (env.TryGetValue("CLAUDE_CONFIG_DIR", out var claude) && claude is not null)
        {
            Copy(script, claude + "/.credentials.json", "$HOME/.claude/.credentials.json");
        }

        if (env.TryGetValue("CODEX_HOME", out var codex) && codex is not null)
        {
            Copy(script, codex + "/auth.json", "$HOME/.codex/auth.json");
        }

        if (plan.Login == ModelLoginSource.SeparateLogin)
        {
            script.Append("[ -e \"$HOME/.claude\" ] && fail \"the owner's agent home is visible\"\n[ -e \"$HOME/.codex\" ] && fail \"the owner's agent home is visible\"\n");
        }

        script.Append("exit 0\n");
        return new SandboxCanary(SandboxReasons.Login, script.ToString());
    }

    // The copy is readable by its owner only at its planned path, and the owner's original login is not writable from inside.
    private static void Copy(StringBuilder script, string copy, string original)
    {
        script.Append(CultureInfo.InvariantCulture, $"f={Quote(copy)}\n[ -r \"$f\" ] || fail \"the login copy is not readable at $f\"\nm=$(stat -c %a \"$f\"); [ \"$m\" = 600 ] || fail \"the login copy has mode $m\"\n");
        // The mask is /dev/null: a device node stays "writable" under a read-only mount, but nothing written reaches the file. What must hold is that the original reads empty and no real file there is writable.
        script.Append(CultureInfo.InvariantCulture, $"o=\"{original}\"\n[ ! -s \"$o\" ] || fail \"the owner's original login is readable\"\n{{ [ -f \"$o\" ] && [ -w \"$o\" ]; }} && fail \"the owner's original login is writable\"\n");
    }

    // A direct connection to a public address fails: the session has a loopback and nothing else. (Through the forwarders is SB-4a's net-forward and the proxy half of this.)
    private static SandboxCanary NetDeny() =>
        new(SandboxReasons.NetDeny, Prelude + "command -v timeout >/dev/null && command -v bash >/dev/null || fail 'no timeout or bash inside the sandbox'\ntimeout 2 bash -c '(exec 3<>/dev/tcp/1.1.1.1/443) 2>/dev/null' && fail 'a direct connection to 1.1.1.1:443 succeeded'\nexit 0\n");
}
