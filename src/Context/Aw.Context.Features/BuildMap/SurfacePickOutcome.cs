using Aw.Context.Core.Overrides;

namespace Aw.Context.Features.BuildMap;

/// <summary>What the product-surface step of a build settled: the override rows to add to the draft and the build's status note, when Jev did not decide.</summary>
/// <param name="Rows">The reject and merge rows (who is 'service:jev').</param>
/// <param name="Note">'product surfaces: not decided (reason)', or null.</param>
internal sealed record SurfacePickOutcome(IReadOnlyList<MapOverride> Rows, string? Note)
{
    /// <summary>No groups, no rows, no note.</summary>
    public static SurfacePickOutcome None { get; } = new([], Note: null);
}
