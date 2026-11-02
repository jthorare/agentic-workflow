namespace Aw.Context.Features.QueryContext;

/// <summary>An item that no stored record answers to: it is unknown if no hit has that tracker key.</summary>
/// <param name="Item">The item as asked.</param>
/// <param name="Suggested">Tracker keys of linked records to ask for instead.</param>
internal sealed record ItemMiss(string Item, IReadOnlyList<string> Suggested);
