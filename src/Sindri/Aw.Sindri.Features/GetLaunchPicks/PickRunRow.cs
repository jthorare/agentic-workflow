namespace Aw.Sindri.Features.GetLaunchPicks;

/// <summary>
/// The fields of a mirrored workflow event's payload that tell how a run ended and which launch opened it (engine spec §5.6):
/// every field optional, so a payload with more still reads.
/// </summary>
/// <param name="Trigger">The trigger kind ('step.completed', 'step.failed', ...).</param>
/// <param name="Run">The run token the trigger is for.</param>
/// <param name="Detail">The rest of the event's data.</param>
public sealed record PickRunRow(string? Trigger = null, string? Run = null, PickRunDetail? Detail = null);
