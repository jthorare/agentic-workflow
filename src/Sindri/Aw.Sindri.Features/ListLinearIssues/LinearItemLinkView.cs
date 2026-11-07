namespace Aw.Sindri.Features.ListLinearIssues;

/// <summary>The Sindri item working an issue.</summary>
/// <param name="Item">The item's id.</param>
/// <param name="ItemState">The item's state word ('running', 'done').</param>
internal sealed record LinearItemLinkView(string Item, string ItemState);
