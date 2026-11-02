namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One action of a <see cref="PagesPreviewPage"/>.</summary>
/// <param name="Id">The action id to save; null lets the server give the name's slug.</param>
/// <param name="Name">The name.</param>
/// <param name="Action">The map action key; null for an owner action.</param>
/// <param name="Concepts">Concept ids it relates to.</param>
internal sealed record PagesPreviewPageAction(string? Id, string Name, string? Action, IReadOnlyList<string> Concepts);
