namespace Aw.Architecture.Tests;

/// <summary>The check behind <see cref="CoreJsonContextRuleTests"/>: which of Core's JSON contexts break .agents/rules/csharp.md (#2496, #2563, #2576).</summary>
internal static class CoreJsonContextRule
{
    // The reviewed list: Core's contexts, each named in csharp.md's second exception. A new one is an edit here and there.
    private static readonly string[] _allowed = ["GraphJsonContext", "GraphOpJsonContext"];

    /// <summary>
    /// The context names that break the rule, given Core's contexts and the rule's text; empty when the rule holds. At any count: a listed one Core no longer
    /// has, so a context folded away leaves no stale entry. With more than one context, also: a context not on the reviewed list, and a listed one the rule
    /// doesn't name as a code span (`Name`). Names are matched whole, never as substrings, so a name the rule mentions for another exception, or part of an
    /// allowed name, doesn't pass.
    /// </summary>
    public static IReadOnlyList<string> Violations(IReadOnlyList<string> contexts, string rule)
    {
        var split = contexts.Count > 1;
        IEnumerable<string> offList = split ? contexts.Where(static c => !_allowed.Contains(c, StringComparer.Ordinal)) : [];
        IEnumerable<string> missing = _allowed.Where(a => !contexts.Contains(a, StringComparer.Ordinal));
        IEnumerable<string> unnamed = split ? _allowed.Where(a => !rule.Contains($"`{a}`", StringComparison.Ordinal)) : [];
        return offList.Concat(missing).Concat(unnamed).Distinct(StringComparer.Ordinal).ToList();
    }
}
