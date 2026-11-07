using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Linear;

namespace Aw.Sindri.Features.GetLinearLookups;

/// <summary>'GET /sindri/linear/lookups/{team}' (Linear hub spec 2026-11-04 §3): the team's states, members, labels and cycles. Owner only.</summary>
/// <param name="Team">The team's key, 'ENG'.</param>
internal sealed record GetLinearLookupsRequest(string Team) : IQuery<LinearLookupsResponse>;
