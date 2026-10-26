using System.Globalization;
using Aw.Context.Features.GetCode;

namespace Aw.Context.Tests.GetCode;

/// <summary>The code fill's reads run in parallel, bounded, and are taken in the original order whatever order they finish in.</summary>
public sealed class FanoutTests
{
    private static readonly int[] _items = [.. Enumerable.Range(0, 24)];

    [Fact, Trait("Lane", "unit")]
    public async Task Results_are_taken_in_item_order_whatever_order_the_reads_finish_in()
    {
        var taken = new List<(int Item, string Result)>();

        // later items finish first
        await Fanout.ForEach(_items, async (item, token) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds((_items.Length - item) * 3), token);
            return "r" + item.ToString(CultureInfo.InvariantCulture);
        }, (item, result) =>
        {
            taken.Add((item, result));
            return true;
        }, degree: 4, TestContext.Current.CancellationToken);

        Assert.Equal(_items.Select(i => (i, "r" + i.ToString(CultureInfo.InvariantCulture))), taken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_more_reads_than_the_degree_are_in_flight()
    {
        var inFlight = 0;
        var peak = 0;

        await Fanout.ForEach(_items, async (item, token) =>
        {
            var now = Interlocked.Increment(ref inFlight);
            int seen;
            while ((seen = Volatile.Read(ref peak)) < now && Interlocked.CompareExchange(ref peak, now, seen) != seen)
            {
            }

            await Task.Delay(10, token);
            _ = Interlocked.Decrement(ref inFlight);
            return item;
        }, (_, _) => true, degree: 3, TestContext.Current.CancellationToken);

        Assert.InRange(peak, 2, 3);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Taking_stops_at_the_first_item_that_says_so()
    {
        var taken = new List<int>();

        await Fanout.ForEach(_items, (item, _) => ValueTask.FromResult(item), (item, _) =>
        {
            taken.Add(item);
            return item < 5;
        }, degree: 4, TestContext.Current.CancellationToken);

        Assert.Equal([0, 1, 2, 3, 4, 5], taken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cancelled_run_hands_back_the_results_in_front_of_the_first_unfinished_read_and_then_throws()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var taken = new List<int>();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Fanout.ForEach(_items, async (item, token) =>
        {
            if (item == 8)
            {
                await cts.CancelAsync();
            }

            await Task.Delay(item < 8 ? 0 : 5_000, token);
            return item;
        }, (item, _) =>
        {
            taken.Add(item);
            return true;
        }, degree: 1, cts.Token));

        Assert.Equal(Enumerable.Range(0, 8), taken);
    }
}
