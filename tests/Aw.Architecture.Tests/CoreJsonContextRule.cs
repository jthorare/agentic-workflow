namespace Aw.Architecture.Tests;

/// <summary>The check behind <see cref="CoreJsonContextRuleTests"/>: which of Core's JSON contexts break .agents/rules/csharp.md (#2496, #2563).</summary>
internal static class CoreJsonContextRule
{
    // The reviewed list: Core's contexts past the first, each named in csharp.md's second exception. A new one is an edit here and there.
    private static readonly string[] _allowed = ["GraphJsonContext", "GraphOpJsonContext"];

    /// <summary>
    /// The context names that break the rule, given Core's contexts and the rule's text; empty when the rule holds. With more than one context: a context not on
    /// the reviewed list, a listed one Core no longer has, and a listed one the rule doesn't name as a code span (`Name`). Names are matched whole, never as
    /// substrings, so a name the rule mentions for another exception, or part of an allowed name, doesn't pass.
    /// </summary>
    public static IReadOnlyList<string> Violations(IReadOnlyList<string> contexts, string rule) =>
        contexts.Count <= 1
            ? []
            : contexts.Where(static c => !_allowed.Contains(c, StringComparer.Ordinal))
                .Concat(_allowed.Where(a => !contexts.Contains(a, StringComparer.Ordinal)))
                .Concat(_allowed.Where(a => !rule.Contains($"`{a}`", StringComparison.Ordinal)))
                .Distinct(StringComparer.Ordinal)
                .ToList();
}
