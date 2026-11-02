namespace Aw.Sindri.Core.Projects.Cards;

/// <summary>A link in a card's sheet: 'Linear' for the project's own page, the owner's links by their url.</summary>
/// <param name="Label">What the link says.</param>
/// <param name="Url">An https url.</param>
public sealed record ProjectLink(string Label, string Url);
