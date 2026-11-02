using Aw.Context.Core.Engine;
using Aw.Context.Core.ScopeGraph;

namespace Aw.Context.Features.GetScopeGraph;

/// <summary>The scoping graph around a set of anchors (issue 1005).</summary>
/// <param name="Query">The anchors, the map, the range, the brief and the caps.</param>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record GetScopeGraphRequest(ScopeGraphQuery Query, ContextScope Scope);
