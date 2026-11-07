namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>The Sindri item working an issue.</summary>
/// <param name="Item">The item id.</param>
/// <param name="ItemState">The item's state word, such as running or done.</param>
internal sealed record LinearItemLink(string Item, string ItemState);
