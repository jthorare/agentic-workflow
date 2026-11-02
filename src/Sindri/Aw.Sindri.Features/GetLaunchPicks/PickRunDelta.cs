namespace Aw.Sindri.Features.GetLaunchPicks;

/// <summary>The post-state delta of a workflow event, as far as the picks read it.</summary>
/// <param name="Frame">The instance's frame after the row.</param>
public sealed record PickRunDelta(PickRunFrame? Frame = null);
