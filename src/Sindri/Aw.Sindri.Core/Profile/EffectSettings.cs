using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The profile's effect keys (engine spec §5.7, §7.2 S1-D11; engine P6 unit (d)): 'workflow.effects', the allowlist
/// the dispatch recheck and RCP-EFFECT-001 read, and 'workflow.dispatchDelay', the undo window before each effect runs.
/// </summary>
/// <param name="Allowlist">The allowlisted effect ids; null when the profile has no 'workflow.effects' (RCP-EFFECT-001 skipped, every recipe effect refused with SND-WF-011).</param>
/// <param name="DispatchDelay">The delay before an effect's activity runs.</param>
public sealed record EffectSettings(IReadOnlySet<BlockId>? Allowlist, TimeSpan DispatchDelay)
{
    /// <summary>'workflow.dispatchDelay' when the profile leaves it out: 10 s (§5.7, the undo window).</summary>
    public static TimeSpan DefaultDispatchDelay { get; } = TimeSpan.FromSeconds(10);

    /// <summary>The effect keys <paramref name="profile"/> sets, the delay's default applied. Engine P6 unit (d).</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The settings.</returns>
    public static EffectSettings Of(SindriProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var workflow = profile.Workflow;
        return new(AllowlistOf(workflow?.Effects), Delay(workflow?.DispatchDelay) ?? DefaultDispatchDelay);
    }

    /// <summary>
    /// Parses 'workflow.dispatchDelay': a whole number and s, m or h, from 5s to 24h, the grammar of a service's cadence
    /// (<see cref="Cadence"/>).
    /// </summary>
    /// <param name="text">The key's text.</param>
    /// <returns>The delay, or null when it isn't one.</returns>
    public static TimeSpan? Delay(string? text) =>
        text is not null && Cadence.Parse(text) is { IsSuccess: true } cadence ? cadence.Value.Interval : null;

    /// <summary>
    /// The allowlist's ids; null when the key is absent. An entry that isn't an 'effect.*' id is left out: the profile
    /// check (<see cref="ProfileRules.Check(SindriProfile)"/>) refuses one before a profile can be approved.
    /// </summary>
    private static HashSet<BlockId>? AllowlistOf(IReadOnlyList<string>? ids) =>
        ids is null ? null : [.. ids.Where(IsEffectId).Select(BlockId.From)];

    /// <summary>Whether <paramref name="entry"/> names an effect block: a block id in the 'effect.' family.</summary>
    /// <param name="entry">The allowlist entry.</param>
    /// <returns>True for 'effect.notify.desk' and the like.</returns>
    public static bool IsEffectId(string entry) =>
        BlockId.TryFrom(entry, out _) && entry.StartsWith("effect.", StringComparison.Ordinal);
}
