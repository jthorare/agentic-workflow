namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>How long a block-runtime Step may run (DT spec §8.2): the descriptor's 'maxDuration', or two minutes when it sets none. The one number the activity's bound and RCP-TIME-003 both read.</summary>
public static class BlockLimits
{
    /// <summary>The bound of a block Step whose descriptor sets no 'maxDuration'.</summary>
    public static readonly TimeSpan Fallback = TimeSpan.FromMinutes(2);

    /// <summary>The longest <paramref name="descriptor"/>'s Step may run.</summary>
    /// <param name="descriptor">The Step's descriptor.</param>
    /// <returns>Its 'maxDuration', else <see cref="Fallback"/>.</returns>
    public static TimeSpan MaxDuration(BlockDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.MaxDuration ?? Fallback;
    }
}
