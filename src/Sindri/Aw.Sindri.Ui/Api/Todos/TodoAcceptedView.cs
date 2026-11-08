namespace Aw.Sindri.Ui.Api.Todos;

/// <summary>A todo write the API accepted (todo spec §4.4).</summary>
/// <param name="Accepted">The command id and the ledger sequence the app sends as X-Sindri-After next.</param>
/// <param name="Id">The todo's "td_…" id.</param>
/// <param name="Version">The version the todo is at now.</param>
/// <param name="Due">The day it read from the typed words, or null.</param>
internal sealed record TodoAcceptedView(CommandAcceptedView Accepted, string Id, long Version, DateOnly? Due);
