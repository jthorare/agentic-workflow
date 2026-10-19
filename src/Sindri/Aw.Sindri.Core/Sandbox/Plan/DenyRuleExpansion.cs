namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The one function that expands the deny rules with the forms a kept rewriting hook turns commands into (<see cref="HookTreatment.KeepDenyRewrites"/>).</summary>
public static class DenyRuleExpansion
{
    private const string Prefix = ":*";

    /// <summary>Adds the rewritten form of every denied Bash command for each kept hook that rewrites commands.</summary>
    /// <param name="rules">The rules.</param>
    /// <param name="hooks">The session's hooks.</param>
    /// <returns>The rules, with the extra forms after the ones they follow.</returns>
    public static IReadOnlyList<InnerRule> Expand(IReadOnlyList<InnerRule> rules, PlanHooks hooks)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(hooks);
        var prefixes = hooks.Kept.Select(HookInventory.Find)
            .Where(h => h is { Treatment: HookTreatment.KeepDenyRewrites })
            .Select(h => h?.RewritePrefix)
            .OfType<string>()
            .Where(prefix => prefix.Length > 0)
            .ToList();
        if (prefixes.Count == 0)
        {
            return rules;
        }

        List<InnerRule> expanded = [];
        foreach (var rule in rules)
        {
            expanded.Add(rule);
            if (rule.Effect != InnerRuleEffect.Deny || !string.Equals(rule.Tool, "Bash", StringComparison.Ordinal))
            {
                continue;
            }

            expanded.AddRange(prefixes.Select(p => rule with { Pattern = p + rule.Pattern }));
            if (rule.Pattern.EndsWith(Prefix, StringComparison.Ordinal) && rule.Pattern[..^Prefix.Length] is var command && command.Contains(' ', StringComparison.Ordinal))
            {
                expanded.Add(rule with { Pattern = "*" + command + "*" });   // why: a rewrite may also move the command (wrappers, 'cd x &&')
            }
        }

        return expanded;
    }
}
