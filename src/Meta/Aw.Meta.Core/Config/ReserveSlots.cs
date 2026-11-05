using System.Text.Json.Serialization;

namespace Aw.Meta.Core.Config;

/// <summary>How many box-wide heavy slots a reserve tier holds: a number, or all of them (heavy.cap in team.json); a tier is still held to the cap less one when applied.</summary>
/// <param name="Count">The slots; null means all.</param>
[JsonConverter(typeof(ReserveSlotsConverter))]
public readonly record struct ReserveSlots(int? Count)
{
    /// <summary>Every slot the host has (a tier holding it is clamped to the cap less one).</summary>
    public static ReserveSlots All { get; } = new(Count: null);

    /// <summary>Resolves the tier against the box-wide cap.</summary>
    /// <param name="cap">The heavy cap.</param>
    /// <returns>The slots, never above the cap and never below zero.</returns>
    public int Resolve(int cap) => Math.Clamp(Count ?? cap, 0, Math.Max(cap, 0));
}
