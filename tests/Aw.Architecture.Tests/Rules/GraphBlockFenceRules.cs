namespace Aw.Architecture.Tests.Rules;

/// <summary>The test-run fence fails closed (spec 2026-11-12 §5.1): a block's constructor takes only types that have a test binding or are read-only.</summary>
public static class GraphBlockFenceRules
{
    /// <summary>The rule id.</summary>
    public const string Id = "ARCH-FENCE-001";

    /// <summary>Checks every constructor parameter of every graph block.</summary>
    /// <param name="blocks">Each block type.</param>
    /// <param name="bound">The types with a test binding.</param>
    /// <param name="readOnly">The read-only types.</param>
    /// <returns>One violation per (block, parameter type) with neither.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<Type> blocks, IReadOnlySet<Type> bound, IReadOnlySet<Type> readOnly) =>
        blocks
            .SelectMany(static block => block.GetConstructors().SelectMany(static c => c.GetParameters()).Select(p => (Block: block, Parameter: p.ParameterType)).Distinct())
            .Where(u => !bound.Contains(u.Parameter) && !readOnly.Contains(u.Parameter))
            .Select(u => new Violation(Id, $"{u.Block.FullName} takes {u.Parameter.FullName}, which has no test binding and is not read-only",
                "add a test version to GraphTestBindings.Bound, or list the type in GraphTestBindings.ReadOnly when it can't write"));
}
