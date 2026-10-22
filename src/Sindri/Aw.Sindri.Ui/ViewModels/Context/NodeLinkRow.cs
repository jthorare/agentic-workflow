namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>One line of the inspector's "Connected to" list: the node at the other end and how it is linked.</summary>
/// <param name="Key">The other node's key.</param>
/// <param name="Label">Its label (the key when the map does not hold it).</param>
/// <param name="Caption">The edge's words and the other node's kind, "part of · feature".</param>
/// <param name="Suggested">Whether the link is not confirmed.</param>
internal sealed record NodeLinkRow(string Key, string Label, string Caption, bool Suggested);
