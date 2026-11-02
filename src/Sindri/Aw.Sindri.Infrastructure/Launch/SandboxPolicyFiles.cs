using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The sandbox half of a Step's policy files (sandbox spec §5, SB-5a ReadOnly): the plan's inner rules in Claude Code's rule syntax, merged into the
/// settings file <see cref="StepPolicyFile"/> renders, and the extra '-c' overrides a sandboxed Codex Step takes after <c lang="text">StepForms</c>' own.
/// </summary>
internal static class SandboxPolicyFiles
{
    private static readonly HashSet<string> _pathTools = new(["Read", "Edit", "Write", "NotebookEdit", "Grep", "Glob"], StringComparer.Ordinal);

    /// <summary>Merges the plan's rules and mode into a Step's permissions: its allow and deny lists come after the Step's own, without repeats; 'ask' rules (attended sessions only) are not rendered.</summary>
    /// <param name="permissions">The Step's own permissions.</param>
    /// <param name="mode">The Step's permission mode (a Plan Step stays in 'plan').</param>
    /// <param name="overlay">The plan's contribution.</param>
    /// <returns>The permissions with the plan's rules, 'dontAsk' (or the plan's own mode) and bypass disabled.</returns>
    internal static ClaudePermissions Merge(ClaudePermissions permissions, StepPermissionMode mode, SandboxPolicyOverlay overlay)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(overlay);
        var allow = Union(permissions.Allow, Rules(overlay.Inner, InnerRuleEffect.Allow));
        var deny = Union(permissions.Deny ?? [], Rules(overlay.Inner, InnerRuleEffect.Deny));
        return permissions with
        {
            Allow = allow,
            Deny = deny.Count == 0 ? null : deny,
            DefaultMode = mode == StepPermissionMode.Plan ? "plan" : overlay.Inner.DefaultMode,
            DisableBypassPermissionsMode = InnerPolicy.DisableBypassPermissions ? "disable" : null,
        };
    }

    /// <summary>The '-c' overrides a sandboxed Codex Step adds: it never asks (nobody is there), and a Step that writes its worktree loses the network.</summary>
    /// <param name="policy">The Step's policy.</param>
    /// <returns>The arguments, to go after <c lang="text">StepForms.Arguments</c>.</returns>
    internal static IReadOnlyList<string> CodexOverrides(StepPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        List<string> args = ["-c", "approval_policy=\"never\""];
        if (policy is { Mode: StepPermissionMode.ReadOnly, WritablePath: not null })
        {
            args.AddRange(["-c", "sandbox_workspace_write.network_access=false"]);
        }

        return args;
    }

    private static List<string> Rules(InnerPolicy inner, InnerRuleEffect effect) =>
        [.. inner.Rules.Where(r => r.Effect == effect).Select(Render)];

    /// <summary>One rule in Claude's syntax: a tool alone, or 'Tool(pattern)', where a file tool's absolute pattern takes the extra '/' of Claude's absolute-path form.</summary>
    private static string Render(InnerRule rule)
    {
        if (rule.Pattern.Length == 0)
        {
            return rule.Tool;
        }

        var pattern = _pathTools.Contains(rule.Tool) && rule.Pattern[0] == '/' ? "/" + rule.Pattern : rule.Pattern;
        return $"{rule.Tool}({pattern})";
    }

    private static List<string> Union(IReadOnlyList<string> first, List<string> second) => [.. first.Concat(second).Distinct(StringComparer.Ordinal)];
}
