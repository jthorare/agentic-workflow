using Aw.Context.Core.WholeMap;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The whole map in one answer (GET /sindri/context/map/whole).</summary>
/// <param name="Detail">'auto', 'nodes' or 'areas', or null for auto (anything else is SND-CONTEXT-006).</param>
/// <param name="Locate">Node keys whose area the answer names (up to 100).</param>
/// <param name="Draft">True to read the draft map.</param>
/// <param name="MapVersion">A retained map version ('v3', '3' or 'v3@a41b07d'), or null for the current one; not valid with <paramref name="Draft"/>.</param>
/// <param name="Repo">A human caller's repo when the profile has more than one, or null.</param>
/// <param name="Area">One area's key to open on its own (its nodes and edges in full, up to the area cap), or null; one the map lacks is SND-CONTEXT-200.</param>
/// <param name="Concept">One concept's key to open on its own (its members and two levels below them), or null; 'concept:other' opens the members no concept takes, one the map lacks is SND-CONTEXT-200, and not together with <paramref name="Area"/> (SND-CONTEXT-006).</param>
public sealed record GetContextWholeMapRequest(string? Detail = null, IReadOnlyList<string>? Locate = null, bool Draft = false, string? MapVersion = null, string? Repo = null, string? Area = null, string? Concept = null) : IQuery<WholeMapView>;
