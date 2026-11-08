using Aw.Context.Core.Engine;

namespace Aw.Context.Features.CheckMap;

/// <summary>The doctor check of the current map; <paramref name="Scope"/> is the approved profile's, or null when none is approved.</summary>
/// <param name="Scope">The scope the map is counted under.</param>
internal sealed record CheckMapRequest(ContextScope? Scope);
