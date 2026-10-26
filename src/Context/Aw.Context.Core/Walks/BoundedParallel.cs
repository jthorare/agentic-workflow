using System.Runtime.ExceptionServices;

namespace Aw.Context.Core.Walks;

/// <summary>
/// Runs independent, read-only work items with a bounded number in flight and hands the results back in input order, so a caller that merges them in that order gets the answer a serial run gives.
/// </summary>
public static class BoundedParallel
{
    /// <summary>Runs <paramref name="work"/> over every item, at most <paramref name="degree"/> at once.</summary>
    /// <typeparam name="TIn">The item type.</typeparam>
    /// <typeparam name="TOut">The result type.</typeparam>
    /// <param name="items">The items.</param>
    /// <param name="work">The work for one item; it must only read shared state.</param>
    /// <param name="degree">The most items in flight; below 2 (or fewer than two items) runs them one at a time.</param>
    /// <param name="cancellationToken">Stops the run; items not yet started are not started.</param>
    /// <returns>One result per item, at the item's index.</returns>
    public static async ValueTask<TOut[]> Map<TIn, TOut>(IReadOnlyList<TIn> items, Func<TIn, CancellationToken, ValueTask<TOut>> work, int degree, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(work);
        var results = new TOut[items.Count];
        if (degree < 2 || items.Count < 2)
        {
            for (var i = 0; i < results.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results[i] = await work(items[i], cancellationToken).ConfigureAwait(false);
            }

            return results;
        }

        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Min(degree, items.Count), CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(Enumerable.Range(0, items.Count), options, async (index, token) => results[index] = await work(items[index], token).ConfigureAwait(false)).ConfigureAwait(false);
        return results;
    }

    /// <summary>Runs <paramref name="work"/> over every item on the calling thread and the pool, at most <paramref name="degree"/> at once.</summary>
    /// <typeparam name="TIn">The item type.</typeparam>
    /// <typeparam name="TOut">The result type.</typeparam>
    /// <param name="items">The items.</param>
    /// <param name="work">The work for one item; it must only read shared state.</param>
    /// <param name="degree">The most items in flight; below 2 (or fewer than two items) runs them one at a time.</param>
    /// <returns>One result per item, at the item's index.</returns>
    public static TOut[] MapSync<TIn, TOut>(IReadOnlyList<TIn> items, Func<TIn, TOut> work, int degree)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(work);
        var results = new TOut[items.Count];
        if (degree < 2 || items.Count < 2)
        {
            for (var i = 0; i < results.Length; i++)
            {
                results[i] = work(items[i]);
            }

            return results;
        }

        try
        {
            _ = Parallel.For(0, results.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(degree, results.Length) }, index => results[index] = work(items[index]));
        }
        catch (AggregateException failed)
        {
            // A serial run throws the work's own exception; so does this one (the first the loop collected when several items failed).
            ExceptionDispatchInfo.Capture(failed.InnerExceptions[0]).Throw();
        }

        return results;
    }
}
