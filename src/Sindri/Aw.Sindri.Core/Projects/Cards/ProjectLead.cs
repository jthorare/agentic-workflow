namespace Aw.Sindri.Core.Projects.Cards;

/// <summary>A card's lead: "you" for a led project, else the lead's name (from Linear: untrusted).</summary>
/// <param name="You">Whether the owner leads it.</param>
/// <param name="Name">The lead's name when it isn't the owner, else null.</param>
public sealed record ProjectLead(bool You, string? Name);
