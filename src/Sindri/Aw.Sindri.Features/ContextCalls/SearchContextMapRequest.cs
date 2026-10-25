using Aw.Context.Core.Search;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Ranked full-text search over the map (GET /sindri/context/search?q=&amp;limit=&amp;area=&amp;repo=&amp;draft=&amp;mapVersion=, or QUERY with this record as the JSON body): node labels, file paths, record titles,
/// decision wording and quotes, best match first. Both transports run the same service, so the same question answers the same bytes.
/// </summary>
/// <param name="Q">The words to find; the last one also matches as a prefix. Blank, or no letters or digits, is SND-CONTEXT-006.</param>
/// <param name="Limit">How many hits to return, 1 to 200, or null for 50.</param>
/// <param name="Area">One area's key to search within, or null for the whole map.</param>
/// <param name="Repo">A human caller's repo when the profile has more than one, or null.</param>
/// <param name="Draft">True to search the draft map.</param>
/// <param name="MapVersion">A retained map version ('v3', '3' or 'v3@a41b07d'), or null for the current one; not valid with <paramref name="Draft"/>.</param>
public sealed record SearchContextMapRequest(string? Q = null, int? Limit = null, string? Area = null, string? Repo = null, bool Draft = false, string? MapVersion = null) : IQuery<MapSearchView>;
