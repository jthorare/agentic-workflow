using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The public form of every 'home.*' event: the row it concerns, when it names one. Titles, facts and brief lines stay in the ledger.</summary>
/// <param name="Id">The row, or null for an event that names none.</param>
public sealed record HomeEventPublic(HomeRowId? Id);
