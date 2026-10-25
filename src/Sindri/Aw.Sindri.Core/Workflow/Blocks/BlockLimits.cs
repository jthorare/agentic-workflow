namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>How long a block-runtime Step may run (DT spec §8.2): the descriptor's 'maxDuration', or thirty days (no real cap) when it sets none: a Step without one is ended by its state timer, not by a fixed limit (owner q93, 2026-10-23). The one number the activity's bound and RCP-TIME-003 both read.</summary>
public static class BlockLimits
{
    /// <summary>The bound of a block Step whose descriptor sets no 'maxDuration'.</summary>
    public static readonly TimeSpan Fallback = TimeSpan.FromDays(30);

    private static readonly TimeSpan _margin = TimeSpan.FromSeconds(1);

    /// <summary>Whether <paramref name="descriptor"/> sets its own 'maxDuration'; a Step that doesn't runs until its state timer ends it.</summary>
    /// <param name="descriptor">The Step's descriptor.</param>
    /// <returns>True when it sets one.</returns>
    public static bool IsExplicit(BlockDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.MaxDuration is not null;
    }

    /// <summary>The longest <paramref name="descriptor"/>'s Step may run.</summary>
    /// <param name="descriptor">The Step's descriptor.</param>
    /// <returns>Its 'maxDuration', else <see cref="Fallback"/>.</returns>
    public static TimeSpan MaxDuration(BlockDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.MaxDuration ?? Fallback;
    }

    /// <summary>
    /// How long the activity of a block run may take: <paramref name="max"/>, or less than the time left on the state timer so the Step's own
    /// 'timeout' ends the run first. After a takeover and handback the timer resumes with only what was left while the Step starts from zero,
    /// so a bound of the full 'maxDuration' would let the timer park the run.
    /// </summary>
    /// <param name="max">The Step's <see cref="MaxDuration"/>.</param>
    /// <param name="timerLeft">What is left on the armed state timer; null when none runs.</param>
    /// <returns>The activity's start-to-close and schedule-to-close bound.</returns>
    public static TimeSpan Bound(TimeSpan max, TimeSpan? timerLeft)
    {
        if (timerLeft is not { } left || left > max + _margin)
        {
            return max;
        }

        var margin = TimeSpan.FromTicks(Math.Min(_margin.Ticks, Math.Max(left.Ticks, 0) / 2));
        return TimeSpan.FromTicks(Math.Max(left.Ticks - margin.Ticks, 1));
    }
}
