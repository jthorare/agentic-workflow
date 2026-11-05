using Aw.Sindri.Infrastructure.Profile;

namespace Aw.Sindri.Infrastructure.Locks;

/// <summary>Where the heavy-job lock lives and how many slots it has (config/lib/locks.sh, the heavy wrapper and Evidence's directory locks read the same variables).</summary>
/// <param name="Lock">The lock's slot 1: AW_HEAVY_JOB_LOCK, or &lt;state root&gt;/locks/heavy-job.lock. Slot k is '&lt;Lock&gt;.k'.</param>
/// <param name="Slots">How many heavy jobs may run at once when settings were read, by the box's one cap rule (<see cref="HeavyCap.Slots"/>): the $AW_STATE_DIR/heavy-slots-cap file, else AW_HEAVY_SLOTS, else 1; a value that is not a whole number of at least 1 means 1. A taker asks <see cref="CurrentSlots"/> instead, which reads the files again.</param>
/// <param name="Held">AW_HEAVY_LOCK_HELD=1: a parent process (the heavy wrapper) already holds a slot, so taking one is a no-op.</param>
internal sealed record HeavyLockSettings(string Lock, int Slots, bool Held)
{
    /// <summary>Reads the cap again (set by <see cref="Read"/>); null means <see cref="Slots"/> stays as given.</summary>
    internal Func<int>? LiveSlots { get; init; }

    /// <summary>Reads the meta reserve again, given the slot count (set by <see cref="Read"/>); null means no reserve.</summary>
    internal Func<int, int>? LiveReserve { get; init; }

    /// <summary>The slot count now: the cap file and AW_HEAVY_SLOTS as they read at this moment, so a cap lowered after serve started applies to the next look.</summary>
    internal int CurrentSlots() => LiveSlots?.Invoke() ?? Slots;

    /// <summary>The slots, counted from slot 1, that only meta may take now (heavy-meta-reserve, at most <paramref name="slots"/> - 1). Sindri's jobs are never meta, so a Sindri taker skips them.</summary>
    /// <param name="slots">The slot count; the reserve is clamped to one less.</param>
    internal int CurrentReserve(int slots) => LiveReserve?.Invoke(slots) ?? 0;

    /// <summary>Reads the settings from the environment, the way config/lib/locks.sh reads the same variables; the slot count comes from <see cref="HeavyCap.Slots"/>.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="stateRoot">The state root (AW_STATE_DIR or ~/.agentic-workflow).</param>
    /// <returns>The settings.</returns>
    internal static HeavyLockSettings Read(Func<string, string?> environment, string stateRoot)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var path = environment("AW_HEAVY_JOB_LOCK") is { Length: > 0 } explicitLock ? explicitLock : Path.Combine(stateRoot, "locks", "heavy-job.lock");
        var slots = HeavyCap.Slots(environment, stateRoot);
        return new HeavyLockSettings(Path.GetFullPath(path), slots, string.Equals(environment("AW_HEAVY_LOCK_HELD"), "1", StringComparison.Ordinal))
        {
            LiveSlots = () => HeavyCap.Slots(environment, stateRoot),
            LiveReserve = count => HeavyCap.Reserve(stateRoot, count),
        };
    }
}
