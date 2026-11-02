namespace Aw.Sindri.Features.GetLaunchPicks;

/// <summary>The frame of a workflow event's delta, as far as the picks read it.</summary>
/// <param name="Run">The open run's token, or null when no run is open.</param>
/// <param name="Actor">On the row that opens a run: the agent's launch id.</param>
public sealed record PickRunFrame(string? Run = null, string? Actor = null);
