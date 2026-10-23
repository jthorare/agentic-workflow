using Aw.Context.Core.Engine;
using Aw.Context.Core.WholeMap;

namespace Aw.Context.Features.GetWholeMap;

/// <summary>The whole map in one answer (Part D).</summary>
/// <param name="Query">The detail, the keys to place, which map and whose repo.</param>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record GetWholeMapRequest(WholeMapQuery Query, ContextScope Scope);
