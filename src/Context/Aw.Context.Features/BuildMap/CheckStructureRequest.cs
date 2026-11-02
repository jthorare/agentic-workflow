using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;

namespace Aw.Context.Features.BuildMap;

/// <summary>The structure measures of a map for a human ('context map checks').</summary>
/// <param name="Source">Which map.</param>
/// <param name="Progress">Told the build's step lines and the heavy-job slot's holder, or null.</param>
/// <param name="Scope">The caller and the profile.</param>
internal sealed record CheckStructureRequest(MapChecksSource Source, IProgress<string>? Progress, ContextScope Scope);
