namespace Aw.Sindri.Core.Profile;

/// <summary>What the box allows that a profile key must stay inside (line spec LN-WF9): read where the profile is read, since Core does no I/O.</summary>
/// <param name="HeavySlots">The box's heavy-slot cap: how many heavy jobs may run at once ($AW_STATE_DIR/heavy-slots-cap, else AW_HEAVY_SLOTS).</param>
public sealed record BoxLimits(int HeavySlots);
