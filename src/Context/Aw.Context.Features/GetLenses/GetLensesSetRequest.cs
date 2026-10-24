using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;

namespace Aw.Context.Features.GetLenses;

/// <summary>The union of the bands around several nodes (schema 2).</summary>
/// <param name="Anchors">Node keys, aliases or labels.</param>
/// <param name="Read">Which map: the current version, the draft or a retained version.</param>
/// <param name="Depth">The depth, or null for the profile's.</param>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record GetLensesSetRequest(IReadOnlyList<string> Anchors, MapRead Read, int? Depth, ContextScope Scope)
{
    /// <summary>The cascade hops as a transport sent them (a comma list), or null for the default three.</summary>
    internal string? Hops { get; init; }
}
