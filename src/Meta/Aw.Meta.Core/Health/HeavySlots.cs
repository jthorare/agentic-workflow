using System.Globalization;

namespace Aw.Meta.Core.Health;

/// <summary>The box-wide heavy slots and how many of them the meta gates should hold, with the queue depth behind it.</summary>
/// <param name="Cap">The slot count the reservation is sized against (heavy.cap in team.json).</param>
/// <param name="Reserve">The slots the meta gates should hold (0 to the cap less one).</param>
/// <param name="Depth">The merge-queue depth the reservation was computed from.</param>
/// <param name="ComputedAt">When it was computed.</param>
/// <param name="Stale">Whether it was computed long enough ago that the queue may have changed since.</param>
public sealed record HeavySlots(int Cap, int Reserve, int Depth, DateTimeOffset ComputedAt, bool Stale = false)
{
    /// <summary>
    /// The argument that makes the installed meta-gate-reserve script keep this many slots. The script maps depths to fixed tiers
    /// (0-2 to 0, 3-4 to 1, 5-6 to 2, 7 or more to its cap), each held to the cap less one, whatever tiers and cap the team config sets, so the queue
    /// depth itself would be wrong for custom tiers. The cap less one (from 3) gets the 7 tier; a count in between has no depth
    /// and gets the 2 tier (never more than asked).
    /// </summary>
    public int ScriptDepth => Reserve switch
    {
        <= 0 => 0,
        1 when Cap > 1 => 3,
        2 when Cap > 2 => 5,
        var slots when slots >= Cap || (slots >= 3 && slots == Cap - 1) => 7,
        _ => 5,
    };

    /// <summary>The line to show: the value, its depth, and the command that applies it to the heavy-job wrapper.</summary>
    /// <returns>The line.</returns>
    public string Line() => string.Create(
        CultureInfo.InvariantCulture,
        $"heavy reservation: {Reserve} of {Cap} slots (queue depth {Depth}) - set it with meta-gate-reserve {ScriptDepth}{(Stale ? $" [stale: computed {ComputedAt:HH:mm}Z; run status or gate-run to recompute]" : string.Empty)}");
}
