using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>Builds the inner policy (sandbox spec §5): target-neutral allow and deny rules that SB-5a renders for each agent CLI.</summary>
internal static class PlanPolicy
{
    private static readonly string[] _writeTools = ["Edit", "Write", "NotebookEdit"];
    private static readonly string[] _readOnlyCommands = ["git status", "git diff", "git log", "git show", "sindri"];

    internal static InnerPolicy Build(PlanContext c, PlanHooks hooks)
    {
        var r = c.Request;
        var allows = r.Attended ? AttendedAllows() : StepAllows(c, r.Policy!);   // why: Build checks that a Step carries its policy
        return new InnerPolicy(r.Attended ? InnerMode.Ask : InnerMode.Refuse, [.. allows, .. DenyRuleExpansion.Expand(DenyRules(c), hooks)]);
    }

    private static List<InnerRule> DenyRules(PlanContext c)
    {
        var state = c.StateDir;
        List<InnerRule> rules = [];
        foreach (var path in ReadDenied(c))
        {
            rules.Add(new InnerRule(InnerRuleEffect.Deny, "Read", path + "/**"));
            rules.Add(new InnerRule(InnerRuleEffect.Deny, "Grep", path));
            rules.Add(new InnerRule(InnerRuleEffect.Deny, "Bash", $"cat {path}*"));
        }

        foreach (var pattern in new[] { "env", "printenv*", "git push:*", "gh:*", "rm -rf /*", $"rm * {state}/locks*", $"mv * {state}/locks*", $"rm * {state}/judge*", $"mv * {state}/judge*" })
        {
            rules.Add(new InnerRule(InnerRuleEffect.Deny, "Bash", pattern));
        }

        return rules;
    }

    private static IEnumerable<string> ReadDenied(PlanContext c) =>
        c.Denied().Concat(c.OwnerDenied()).Select(d => d.Path).Concat(c.Request.Login?.DeniedReads ?? []).Distinct(StringComparer.Ordinal);

    private static List<InnerRule> AttendedAllows() =>
        [Allow("Read", string.Empty), Allow("Glob", string.Empty), Allow("Grep", string.Empty), Ask("Bash"), Ask("Edit"), Ask("Write")];

    private static InnerRule Ask(string tool) => new(InnerRuleEffect.Ask, tool, string.Empty);

    private static List<InnerRule> StepAllows(PlanContext c, StepPolicy policy)
    {
        List<string> roots = c.WritablePath is { } worktree ? [worktree, c.Request.Paths.RunDirectory] : [c.Request.Paths.RunDirectory];
        List<InnerRule> allows = policy.AllowedTools.Count > 0 ? [.. policy.AllowedTools.SelectMany(t => FromTool(t, roots, c.WriteScope)).Distinct()] : [.. Defaults(c, roots)];
        allows.Add(Allow("mcp__sindri-step__*", string.Empty));
        return allows;
    }

    private static IEnumerable<InnerRule> Defaults(PlanContext c, List<string> roots)
    {
        yield return Allow("Read", string.Empty);
        yield return Allow("Glob", string.Empty);
        yield return Allow("Grep", string.Empty);
        foreach (var root in roots)
        {
            yield return Allow("Edit", root + "/**");
            yield return Allow("Write", root + "/**");
        }

        if (c.WriteScope)
        {
            yield return Allow("Bash", string.Empty);
            yield break;
        }

        foreach (var command in _readOnlyCommands)
        {
            yield return Allow("Bash", command + ":*");
        }
    }

    private static IEnumerable<InnerRule> FromTool(string tool, List<string> roots, bool writeScope)
    {
        var open = tool.IndexOf('(', StringComparison.Ordinal);
        var parenthesized = open > 0 && tool.EndsWith(')');
        var name = parenthesized ? tool[..open] : tool;
        var pattern = parenthesized ? tool[(open + 1)..^1] : string.Empty;
        if (string.Equals(name, "Bash", StringComparison.Ordinal) && !writeScope)
        {
            return ReadOnlyBash(pattern);
        }

        return _writeTools.Contains(name, StringComparer.Ordinal) ? WriteTool(name, pattern, roots) : [Allow(name, pattern)];
    }

    // A read-only Step only tightens: a bare Bash is the whole read-only set, a pattern stays only when it is inside that set, as narrow as asked.
    private static IEnumerable<InnerRule> ReadOnlyBash(string pattern)
    {
        if (pattern.Length == 0)
        {
            return _readOnlyCommands.Select(cmd => Allow("Bash", cmd + ":*"));
        }

        var prefix = pattern.EndsWith(":*", StringComparison.Ordinal) ? pattern[..^2] : pattern.EndsWith(" *", StringComparison.Ordinal) ? pattern[..^2] : pattern;   // 'cmd:*', 'cmd *' and the exact 'cmd'
        return _readOnlyCommands.Any(cmd => string.Equals(prefix, cmd, StringComparison.Ordinal) || prefix.StartsWith(cmd + " ", StringComparison.Ordinal))
            ? [Allow("Bash", pattern)]
            : [];
    }

    // A bare write tool is the roots; an explicit pattern is kept only when it is clean (no '..') and inside a root.
    private static IEnumerable<InnerRule> WriteTool(string name, string pattern, List<string> roots)
    {
        if (pattern.Length == 0)
        {
            return roots.Select(root => Allow(name, root + "/**"));
        }

        var clean = !pattern.Split('/').Any(s => string.Equals(s, "..", StringComparison.Ordinal) || string.Equals(s, ".", StringComparison.Ordinal));
        return clean && roots.Exists(root => pattern.StartsWith(root + "/", StringComparison.Ordinal)) ? [Allow(name, pattern)] : [];
    }

    private static InnerRule Allow(string tool, string pattern) => new(InnerRuleEffect.Allow, tool, pattern);
}
