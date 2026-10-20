namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The recipe validator (spec §9.1): pure, in-process, and the same for every surface. It runs the rule groups in
/// order (schema, graph, wiring, blocks, human trigger, capability, registry) and returns every finding of every group. A recipe passes when
/// no finding is an <see cref="FindingSeverity.Error"/>.
/// </summary>
public static class RecipeValidator
{
    /// <summary>The rule groups, in the order they run. Each P1 unit owns one file.</summary>
    internal static IReadOnlyList<IRecipeRules> Groups { get; } =
    [
        new SchemaRules(),
        new GraphRules(),
        new WiringRules(),
        new ChoiceRules(),
        new DeclarationRules(),
        new BlockRules(),
        new HumanTriggerRules(),
        new CapabilityRules(),
        new RegistryRules(),
    ];

    /// <summary>Validates <paramref name="document"/> against <paramref name="context"/>.</summary>
    /// <param name="document">The bound recipe.</param>
    /// <param name="context">The catalog, profile facts, child resolver and base.</param>
    /// <returns>Every finding, group by group.</returns>
    public static IReadOnlyList<RecipeFinding> Validate(RecipeDocument document, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        return [.. Groups.SelectMany(g => g.Check(document, context))];
    }

    /// <summary>
    /// The schema rules alone, without RCP-HASH-001 (spec §3.1: every schema error is reported, not just the first). The
    /// reader runs them on a recipe whose only reader findings are unknown fields, bound with those left out, so a
    /// misspelt field doesn't hide the schema errors only the bound recipe shows; such a recipe is never hashed or
    /// validated further.
    /// </summary>
    /// <param name="document">A recipe bound with its unknown fields left out.</param>
    /// <returns>The schema rules' findings.</returns>
    public static IReadOnlyList<RecipeFinding> CheckSchema(RecipeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return [.. SchemaRules.Structure(document.Recipe, document.Source)];
    }

    /// <summary>Whether <paramref name="findings"/> pass: none is an error.</summary>
    /// <param name="findings">A validation's findings.</param>
    /// <returns>True when no finding is an error.</returns>
    public static bool Passes(IEnumerable<RecipeFinding> findings) =>
        findings.All(f => f.Severity != FindingSeverity.Error);
}
