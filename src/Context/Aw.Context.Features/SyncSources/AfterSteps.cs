using Aw.Context.Core.Results;

namespace Aw.Context.Features.SyncSources;

/// <summary>Chains the steps that follow one feed's tick: each runs on the row the one before returned, in the order given.</summary>
internal static class AfterSteps
{
    /// <summary>The steps as one.</summary>
    /// <param name="steps">The steps in order; null entries (a feed that has none of that kind) are left out.</param>
    /// <returns>The chain, or null when there is no step.</returns>
    public static Func<SourceSync, ValueTask<SourceSync>>? Compose(params Func<SourceSync, ValueTask<SourceSync>>?[] steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var present = steps.OfType<Func<SourceSync, ValueTask<SourceSync>>>().ToArray();
        return present.Length switch
        {
            0 => null,
            1 => present[0],
            _ => row => Chain(row, present),
        };
    }

    private static async ValueTask<SourceSync> Chain(SourceSync row, Func<SourceSync, ValueTask<SourceSync>>[] steps)
    {
        foreach (var step in steps)
        {
            row = await step(row).ConfigureAwait(false);
        }

        return row;
    }
}
