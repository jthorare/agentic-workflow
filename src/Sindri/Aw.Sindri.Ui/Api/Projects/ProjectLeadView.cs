namespace Aw.Sindri.Ui.Api.Projects;

/// <summary>A project's lead (projects page spec §6.2).</summary>
/// <param name="You">True when the lead is the signed-in Linear viewer.</param>
/// <param name="Name">The lead's name; null when it is the viewer (the page says "You").</param>
internal sealed record ProjectLeadView(bool You, string? Name);
