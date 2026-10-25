namespace Aw.Context.Core.Map;

/// <summary>What the publish rules read from the store: the hold, the latest draft, the current version and the commits it was built at (service use; sessions read <see cref="MapPublishView"/>).</summary>
/// <param name="Hold">The hold, or null when no draft is held.</param>
/// <param name="LatestDraft">The latest draft's number, or null when there is none.</param>
/// <param name="Current">The current version's number, or null when no map has been promoted yet.</param>
/// <param name="CurrentHeads">The current version's heads; empty for one built before heads were recorded.</param>
public sealed record MapPublishState(MapHold? Hold, int? LatestDraft, int? Current, IReadOnlyList<MapHead> CurrentHeads)
{
    /// <summary>The hash of the map rules the current version was built under, or null when none was recorded (a map built before issue 1041).</summary>
    public string? CurrentRuleset { get; init; }
}
