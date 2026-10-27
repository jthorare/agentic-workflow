namespace Aw.Context.Core.WholeMap;

/// <summary>
/// The limits of a whole-map answer (Part D, §2.3). Constants of the code, not profile keys: changing them is a reviewed code change.
/// A host may register another instance (a test, to see a small map go over the budget).
/// </summary>
/// <param name="NodeBudget">The most nodes a 'nodes' answer carries; a bigger map answers areas.</param>
/// <param name="AreaCap">How many nodes an area may hold before the area assigner replaces its feature by its children (a split, never a cut: every node is still listed).</param>
public sealed record WholeMapRules(int NodeBudget = 500, int AreaCap = 5_000)
{
    /// <summary>The most keys one question may ask to place.</summary>
    public const int MaxLocate = 1_000;

    /// <summary>The limits the owner's spec sets.</summary>
    public static WholeMapRules Default { get; } = new();
}
