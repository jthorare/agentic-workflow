namespace Aw.Context.Features.GetCode;

/// <summary>
/// Runs the independent reads of the code fill (a git call or an index call each) with bounded parallelism and hands the results back in the original order, so the answer is the one a sequential run gives.
/// When the token ends the run (the deadline), the results that finished in front of the first unfinished item are still handed back before the cancellation is thrown, as the sequential run kept what it had.
/// </summary>
internal static class Fanout
{
    /// <summary>
    /// The most reads in flight at once: each is a git subprocess or an index query, so a few keep the box busy without starving the other fills that run at the same time.
    /// </summary>
    internal static int Degree { get; } = Math.Min(4, Environment.ProcessorCount);

    /// <summary>Reads every item with <paramref name="read"/>, then gives each result to <paramref name="take"/> in item order until it returns false.</summary>
    internal static ValueTask ForEach<TItem, TResult>(IReadOnlyList<TItem> items, Func<TItem, CancellationToken, ValueTask<TResult>> read, Func<TItem, TResult, bool> take, CancellationToken cancellationToken) =>
        ForEach(items, read, take, Degree, cancellationToken);

    /// <summary>As above, with at most <paramref name="degree"/> reads in flight.</summary>
    internal static async ValueTask ForEach<TItem, TResult>(IReadOnlyList<TItem> items, Func<TItem, CancellationToken, ValueTask<TResult>> read, Func<TItem, TResult, bool> take, int degree, CancellationToken cancellationToken)
    {
        var slots = new TResult?[items.Count];
        var done = new bool[items.Count];
        try
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, degree), CancellationToken = cancellationToken };
            await Parallel.ForEachAsync(Enumerable.Range(0, items.Count), options, async (index, token) =>
            {
                slots[index] = await read(items[index], token).ConfigureAwait(false);
                Volatile.Write(ref done[index], true);
            }).ConfigureAwait(false);
        }
        finally
        {
            for (var i = 0; i < items.Count && Volatile.Read(ref done[i]); i++)
            {
                // why: a slot is written before its flag, so a flagged slot holds a result; for a reference TResult it is not null.
                if (!take(items[i], slots[i]!))
                {
                    break;
                }
            }
        }
    }
}
