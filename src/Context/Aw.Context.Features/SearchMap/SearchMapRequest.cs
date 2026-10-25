using Aw.Context.Core.Engine;
using Aw.Context.Core.Search;

namespace Aw.Context.Features.SearchMap;

/// <summary>A ranked full-text search over a map.</summary>
/// <param name="Query">The words, the limit, the area and repo, and which map.</param>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record SearchMapRequest(MapSearchQuery Query, ContextScope Scope);
