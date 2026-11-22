using System.Text.RegularExpressions;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>Reads the {{node.field}} references in a block's example text, to tell whether one names a step the workflow being edited lacks (#3944).</summary>
internal static partial class ExampleReferences
{
    // why: the roots the graph validator reads as scope, not as a step (GraphValidator.References in Aw.Sindri.Core, which the Ui does not reference); change both together
    private static readonly HashSet<string> _scopeRoots = new(["trigger", "item", "itemIndex", "workflow"], StringComparer.Ordinal);

    /// <summary>The roots a reference may start with that are not steps: trigger, item, itemIndex, workflow.</summary>
    internal static IReadOnlySet<string> ScopeRoots => _scopeRoots;

    /// <summary>True when the text holds a reference whose root is neither a scope root (trigger, item, itemIndex, workflow) nor one of the workflow's step ids.</summary>
    /// <param name="text">The field's text as the form shows it.</param>
    /// <param name="steps">The ids of the workflow's steps.</param>
    /// <returns>True when a reference names a step the workflow lacks.</returns>
    internal static bool NamesMissingStep(string text, IReadOnlySet<string> steps) =>
        Reference.Matches(text).Select(static m => m.Groups["ref"].Value.Split('.', 2)[0]).Any(root => !_scopeRoots.Contains(root) && !steps.Contains(root));

    /// <summary>True when a field of a new step names a step the new one can't read. Stub until the fix: the old rule.</summary>
    /// <param name="type">The block type.</param>
    /// <param name="field">The field's name.</param>
    /// <param name="text">The field's text as the form shows it.</param>
    /// <param name="steps">The steps the new one may read.</param>
    /// <returns>True when a reference names a step outside <paramref name="steps"/>.</returns>
    internal static bool NamesStepNotUpstream(string type, string field, string text, IReadOnlySet<string> steps) =>
        type.Length + field.Length >= 0 && NamesMissingStep(text, steps);

    // why: the same pattern as Template.Reference in Aw.Sindri.Core/Graph/Interpolation/Template.cs; change both together
    [GeneratedRegex("\\{\\{\\s*(?<ref>[^{}]*?)\\s*\\}\\}", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Reference { get; }
}
