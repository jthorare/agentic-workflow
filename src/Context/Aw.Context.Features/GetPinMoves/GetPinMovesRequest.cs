using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;

namespace Aw.Context.Features.GetPinMoves;

/// <summary>Reads what each pin gained and lost between two map versions.</summary>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
/// <param name="From">The earlier version.</param>
/// <param name="To">The later version.</param>
internal sealed record GetPinMovesRequest(ContextScope Scope, MapVersion From, MapVersion To);
