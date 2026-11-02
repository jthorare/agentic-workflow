namespace Aw.Context.Infrastructure.Labeling;

/// <summary>Items grouped into packs, in the order given, and the items too big to go in any pack.</summary>
/// <typeparam name="T">What is packed.</typeparam>
/// <param name="Packs">The packs, each non-empty.</param>
/// <param name="Unfit">The items that alone exceed the budget.</param>
internal sealed record PackPlan<T>(IReadOnlyList<IReadOnlyList<T>> Packs, IReadOnlyList<T> Unfit);
