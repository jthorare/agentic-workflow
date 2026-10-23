namespace Aw.Context.Core.WholeMap;

/// <summary>
/// The limits of a whole-map answer (Part D, §2.3). Constants of the code, not profile keys: changing them is a reviewed code change.
/// A host may register another instance (a test, to see a small map go over the budget).
/// </summary>
/// <param name="NodeBudget">The most nodes a 'nodes' answer carries; a bigger map answers areas.</param>
/// <param name="AreaCap">The most nodes one opened area carries.</param>
/// <param name="AreasCap">The most areas an answer lists.</param>
public sealed record WholeMapRules(int NodeBudget = 500, int AreaCap = 500, int AreasCap = 500)
{
    /// <summary>The most keys one question may ask to place.</summary>
    public const int MaxLocate = 100;

    /// <summary>The limits the owner's spec sets.</summary>
    public static WholeMapRules Default { get; } = new();
}
