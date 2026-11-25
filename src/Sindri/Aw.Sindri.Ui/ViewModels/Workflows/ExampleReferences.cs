using System.Text.RegularExpressions;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>Reads the references in a block's example text, to tell whether one names a step a new step can't read (#3944), and makes the example a hint that names none (#4272).</summary>
internal static partial class ExampleReferences
{
    /// <summary>The step id a hint shows in place of a step the new step can't read.</summary>
    internal const string StepPlaceholder = "<step id>";

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
        ForeignRoots(type, field, text, steps).Any();

    /// <summary>
    /// The hint a new step's field shows: the example text, with every step the new one can't read swapped for <paramref name="near"/>, the step it follows (#4330), so the hint
    /// teaches a reference the save accepts; a field that step's block lacks becomes its first output. With no such step, or one with no outputs, the step shows as "&lt;step id&gt;" (#4272).
    /// </summary>
    /// <param name="type">The block type.</param>
    /// <param name="field">The field's name.</param>
    /// <param name="text">The example's text for the field.</param>
    /// <param name="steps">The steps the new one may read: the one it follows and every step upstream of that one.</param>
    /// <param name="near">The step the new one follows, with its block's outputs, or null for none.</param>
    /// <returns>The hint; the text unchanged when every reference is a scope root or one of <paramref name="steps"/>.</returns>
    internal static string Hint(string type, string field, string text, IReadOnlySet<string> steps, HintStep? near = null)
    {
        var foreign = ForeignRoots(type, field, text, steps).ToHashSet(StringComparer.Ordinal);
        if (foreign.Count == 0)
        {
            return text;
        }

        var hint = Reference.Replace(text, m => Braced(m, foreign, near));
        return IsCondition(type, field)
            ? BareRef.Replace(hint, m => foreign.Contains(m.Groups["root"].Value) ? Named(m.Groups["field"].Success ? m.Groups["field"].Value : null, near) : m.Value)
            : hint;
    }

    private static IEnumerable<string> ForeignRoots(string type, string field, string text, IReadOnlySet<string> steps) =>
        (IsCondition(type, field) ? ConditionReferences(text) : Reference.Matches(text).Select(static m => m.Groups["ref"].Value))
            .Select(static r => r.Split('.', 2)[0])
            .Where(root => !_scopeRoots.Contains(root) && !steps.Contains(root));

    // A braced reference "{{ agent.summary }}" whose root is foreign, with the reference renamed; the spacing inside the braces stays.
    private static string Braced(Match match, HashSet<string> foreign, HintStep? near)
    {
        var reference = match.Groups["ref"];
        var parts = reference.Value.Split('.', 2);
        if (parts[0].Length == 0 || !foreign.Contains(parts[0]))
        {
            return match.Value;
        }

        var at = reference.Index - match.Index;
        return string.Concat(match.Value.AsSpan(0, at), Named(parts.Length > 1 ? parts[1] : null, near), match.Value.AsSpan(at + reference.Length));
    }

    // A foreign reference's new text: the step the new one follows and a field its block has, or "<step id>" and the example's field when there is no such step.
    private static string Named(string? rest, HintStep? near)
    {
        if (near is not { Outputs.Count: > 0 })
        {
            return rest is null ? StepPlaceholder : StepPlaceholder + "." + rest;
        }

        if (rest is null)
        {
            return near.Id;
        }

        return near.Outputs.Contains(rest.Split('.', 2)[0], StringComparer.Ordinal) ? near.Id + "." + rest : near.Id + "." + near.Outputs[0];
    }

    // why: the validator reads only a condition block's "when" with the condition parser (GraphValidator.IsCondition in Aw.Sindri.Core); change both together
    private static bool IsCondition(string type, string field) =>
        string.Equals(type, "condition", StringComparison.Ordinal) && string.Equals(field, "when", StringComparison.Ordinal);

    // why: the same pattern as Template.Reference in Aw.Sindri.Core/Graph/Interpolation/Template.cs; change both together
    [GeneratedRegex("\\{\\{\\s*(?<ref>[^{}]*?)\\s*\\}\\}", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Reference { get; }

    // why: a bare condition word as the tokenizer reads one (ExampleReferences.Condition.cs Word): its root before the first dot and the field after it; a word after a dot is a field, not a root.
    // Hint swaps only roots ConditionReferences already called foreign, so a quoted literal holding the same word is the one miss, which is fine for a hint
    [GeneratedRegex("(?<![A-Za-z0-9_.<-])(?<root>[A-Za-z_][A-Za-z0-9_-]*)(\\.(?<field>[A-Za-z_][A-Za-z0-9_-]*))?", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BareRef { get; }
}
