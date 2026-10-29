namespace Aw.Sindri.Ui.Api.Projects;

/// <summary>One link on a project card: the Linear link first, then the owner's.</summary>
/// <param name="Label">The words shown for the link.</param>
/// <param name="Url">Where it goes.</param>
internal sealed record ProjectLinkView(string Label, string Url);
