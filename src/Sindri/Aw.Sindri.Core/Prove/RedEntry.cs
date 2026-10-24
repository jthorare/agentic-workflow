namespace Aw.Sindri.Core.Prove;

/// <summary>One item's entry of <see cref="RedTestsFact"/>.</summary>
/// <param name="Item">The key.</param>
/// <param name="Tests">The tests it names.</param>
/// <param name="AppOnly">Whether it records an app-only signal instead of (or beside) tests.</param>
/// <param name="Commit">The entry's red commit; null when it names none.</param>
/// <param name="Problem">Why the entry is in none of the accepted shapes, naming the item; null otherwise.</param>
public sealed record RedEntry(string Item, IReadOnlyList<RedTest> Tests, bool AppOnly, string? Commit, string? Problem);
