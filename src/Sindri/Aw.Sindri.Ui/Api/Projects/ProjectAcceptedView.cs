namespace Aw.Sindri.Ui.Api.Projects;

/// <summary>A project write the API accepted (projects page spec §5.3).</summary>
/// <param name="Accepted">The command id and the ledger sequence the app sends as X-Sindri-After next.</param>
/// <param name="Project">The project's "prj_…" id, or the Linear key for a hide.</param>
/// <param name="Version">The version the card is at now.</param>
/// <param name="Name">What to call it in a sentence ("Checkout rewrite hidden").</param>
internal sealed record ProjectAcceptedView(CommandAcceptedView Accepted, string Project, long Version, string Name);
