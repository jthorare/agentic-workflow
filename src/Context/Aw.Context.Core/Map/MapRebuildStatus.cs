namespace Aw.Context.Core.Map;

/// <summary>Where the map's rebuild stands.</summary>
/// <param name="Phase">The state.</param>
public sealed record MapRebuildStatus(MapRebuildPhase Phase)
{
    /// <summary>No refresh runs and nothing blocks one: the service starts it on its next tick, or the map is current.</summary>
    public static MapRebuildStatus Idle { get; } = new(MapRebuildPhase.Idle);

    /// <summary>The latest progress line of the running refresh (<c xml:lang="en">extracting 4,200 of 9,111 files</c>), or null before it reports one.</summary>
    public string? Progress { get; init; }

    /// <summary>When the backoff after failed refreshes ends; set only for <see cref="MapRebuildPhase.Backoff"/>.</summary>
    public DateTimeOffset? RetryAt { get; init; }

    /// <summary>Whether a refresh is queued or running.</summary>
    public bool Building => Phase == MapRebuildPhase.Building;
}
