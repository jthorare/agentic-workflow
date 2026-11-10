namespace Aw.Architecture.Tests;

/// <summary>The check behind <see cref="CoreJsonContextRuleTests"/>: which of Core's JSON contexts break .agents/rules/csharp.md (#2496, #2563).</summary>
internal static class CoreJsonContextRule
{
    /// <summary>The context names that break the rule, given Core's contexts and the rule's text; empty when the rule holds.</summary>
    public static IReadOnlyList<string> Violations(IReadOnlyList<string> contexts, string rule) =>
        contexts.Count <= 1 ? [] : contexts.Where(c => !rule.Contains(c, StringComparison.Ordinal)).ToList();
}
