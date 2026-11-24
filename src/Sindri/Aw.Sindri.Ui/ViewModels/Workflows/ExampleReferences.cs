using System.Text.RegularExpressions;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>Reads the references in a block's example text, to tell whether one names a step a new step can't read (#3944).</summary>
internal static partial class ExampleReferences
{
    // why: the roots the graph validator reads as scope, not as a step (GraphValidator.References in Aw.Sindri.Core, which the Ui does not reference); change both together
    private static readonly HashSet<string> _scopeRoots = new(["trigger", "item", "itemIndex", "workflow"], StringComparer.Ordinal);

    /// <summary>The roots a reference may start with that are not steps: trigger, item, itemIndex, workflow.</summary>
    internal static IReadOnlySet<string> ScopeRoots => _scopeRoots;

    /// <summary>
    /// True when a field of a new step names a step the new one can't read: a reference whose root is neither a scope root nor one of <paramref name="steps"/>.
    /// A template field reads {{node.field}}; a condition's "when" also reads bare words (agent.drifted), as the graph validator does (GraphValidator.References: IsCondition).
    /// </summary>
    /// <param name="type">The block type.</param>
    /// <param name="field">The field's name.</param>
    /// <param name="text">The field's text as the form shows it.</param>
    /// <param name="steps">The steps the new one may read: the one it follows and every step upstream of that one.</param>
    /// <returns>True when a reference names a step outside <paramref name="steps"/>.</returns>
    internal static bool NamesStepNotUpstream(string type, string field, string text, IReadOnlySet<string> steps) =>
        (IsCondition(type, field) ? ConditionReferences(text) : Reference.Matches(text).Select(static m => m.Groups["ref"].Value))
            .Select(static r => r.Split('.', 2)[0])
            .Any(root => !_scopeRoots.Contains(root) && !steps.Contains(root));

    // why: the validator reads only a condition block's "when" with the condition parser (GraphValidator.IsCondition in Aw.Sindri.Core); change both together
    private static bool IsCondition(string type, string field) =>
        string.Equals(type, "condition", StringComparison.Ordinal) && string.Equals(field, "when", StringComparison.Ordinal);

    // why: the same pattern as Template.Reference in Aw.Sindri.Core/Graph/Interpolation/Template.cs; change both together
    [GeneratedRegex("\\{\\{\\s*(?<ref>[^{}]*?)\\s*\\}\\}", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Reference { get; }
}
