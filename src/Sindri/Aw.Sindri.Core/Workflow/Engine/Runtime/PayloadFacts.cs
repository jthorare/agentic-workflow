using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The facts a recipe declares 'payload' (spec §3.2): JSON a later Step reads as a structure, so an agent that writes one must write a JSON object or array.</summary>
public static class PayloadFacts
{
    // What a payload's fields are, for the facts whose readers need more than "JSON": appended to the shape line of every Step that writes the fact.
    private static readonly Dictionary<string, string> _shapes = new(StringComparer.Ordinal)
    {
        ["fact:design"] = "Shape: { \"spec\": \"<the whole design as Markdown, following the style pack>\", \"sections\": [...], \"coverage\": {...}, \"decisions\": [...], \"deferred\": [...], \"resolutions\": [...], \"signals\": [...], \"contracts\": [...], \"acceptance\": {...}, \"escapes\": [...], \"runs\": [...] }; the spec text is required (it becomes spec.md), and the design checks read each other field by name, so keep the ones already in the fact.",
        ["fact:redTests"] = "Shape: one key per acceptance item, and nothing else at the top level (no \"commit\", \"file\" or \"tests\" beside the items): { \"AC-1\": { \"tests\": [\"<test id>\"], \"commit\": \"<red commit sha>\" }, \"AC-2\": { \"tests\": [{ \"id\": \"<test id>\", \"path\": \"<test file>\" }], \"commit\": \"<red commit sha>\" }, \"AC-3\": { \"appOnly\": { \"signal\": \"<signal id>\" }, \"commit\": \"<red commit sha>\" } }; write the commit on every item, the same sha on each (the commit of the failing tests); an item proven only in the running app has \"appOnly\" instead of \"tests\".",
    };

    private const string Fix = "write the structure itself, with any text inside a field, and report again";

    /// <summary>The facts <paramref name="recipe"/> declares with type 'payload', in its inputs and outputs.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <returns>The facts.</returns>
    public static IReadOnlySet<FactKey> DeclaredIn(Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return recipe.Inputs.Select(static i => (i.Key, i.Type)).Concat(recipe.Outputs.Select(static o => (o.Key, o.Type)))
            .Where(static f => string.Equals(f.Type, FactTypes.Payload, StringComparison.Ordinal))
            .Select(static f => f.Key)
            .ToHashSet();
    }

    /// <summary>Why <paramref name="content"/> can't be the payload <paramref name="fact"/>, in words an agent can fix; null when it is a JSON object or array.</summary>
    /// <param name="fact">The fact.</param>
    /// <param name="content">What was written.</param>
    /// <returns>The refusal text, or null.</returns>
    public static string? Violation(FactKey fact, ReadOnlySpan<byte> content)
    {
        try
        {
            using var parsed = JsonDocument.Parse(content.ToArray());
            return parsed.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                ? null
                : $"{fact.Value} is a payload, so it must be a JSON object or array, and it holds a JSON {parsed.RootElement.ValueKind.ToString().ToLowerInvariant()} (fix: {Fix})";
        }
        catch (JsonException)   // boundary: the content is what an agent wrote
        {
            return $"{fact.Value} is a payload, so it must be a JSON object or array, and it isn't JSON at all (fix: {Fix})";
        }
    }

    /// <summary>The brief line that states <paramref name="fact"/>'s shape.</summary>
    /// <param name="fact">The fact.</param>
    /// <returns>The line.</returns>
    public static string Instruction(FactKey fact) =>
        $"{fact.Value} must hold a JSON object or array, not prose or Markdown; put any text inside a field." + (_shapes.TryGetValue(fact.Value, out var shape) ? " " + shape : string.Empty);

    /// <summary>The payload facts <paramref name="descriptor"/>'s Step writes under <paramref name="recipe"/>: its declared outputs the recipe types 'payload', in the descriptor's order.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="descriptor">The Step's descriptor.</param>
    /// <returns>The facts.</returns>
    public static IReadOnlyList<FactKey> OutputsOf(Recipe recipe, BlockDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var declared = DeclaredIn(recipe);
        return [.. descriptor.Outputs.Select(static o => o.Fact).Where(declared.Contains)];
    }

    /// <summary>The Step's instruction lines with the shape of each payload output added after them.</summary>
    /// <param name="instructions">What the Step is told; null for none.</param>
    /// <param name="payload">The payload facts it writes; null for none.</param>
    /// <returns>The lines; null when there are none.</returns>
    public static IReadOnlyList<string>? WithShapes(IReadOnlyList<string>? instructions, IReadOnlyList<FactKey>? payload) =>
        payload is not { Count: > 0 } ? instructions : [.. instructions ?? [], .. payload.Select(Instruction)];
}
