using Aw.Context.Core.Map;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// POST /sindri/context/map/checks: the structure measures of a map and the five largest offenders (map structure spec §4.3). Human-only: the output lists feature keys.
/// <see cref="MapChecksSource.Build"/> extracts in memory with no model call, saves nothing and holds a heavy-job slot while it reads.
/// </summary>
/// <param name="Source">The current map, the open draft, or the map a build would save.</param>
public sealed record CheckContextMapStructureRequest(MapChecksSource Source = MapChecksSource.Current) : ICommand<MapChecksView>;
