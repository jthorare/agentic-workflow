using System.Globalization;
using Aw.Context.Core.Walks;

namespace Aw.Context.Tests.Walks;

/// <summary>The bounded parallel map hands results back in input order at any degree, runs serially at degree 1 and honours cancellation.</summary>
public sealed class BoundedParallelTests
{
    private static readonly int[] _items = [.. Enumerable.Range(0, 12)];

    private static string[] Expected => [.. _items.Select(i => "r" + i.ToString(CultureInfo.InvariantCulture))];

    [Fact, Trait("Lane", "unit")]
    public async Task Results_come_back_in_input_order_even_when_later_items_finish_first()
    {
        var results = await BoundedParallel.Map(_items, async (item, token) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds((_items.Length - item) * 5), token);
            return "r" + item.ToString(CultureInfo.InvariantCulture);
        }, degree: 8, TestContext.Current.CancellationToken);

        Assert.Equal(Expected, results);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Degree_1_runs_one_item_at_a_time()
    {
        var inFlight = 0;
        var peak = 0;

        var results = await BoundedParallel.Map(_items, async (item, token) =>
        {
            Track(ref inFlight, ref peak, +1);
            await Task.Delay(TimeSpan.FromMilliseconds(2), token);
            Track(ref inFlight, ref peak, -1);
            return "r" + item.ToString(CultureInfo.InvariantCulture);
        }, degree: 1, TestContext.Current.CancellationToken);

        Assert.Equal(1, peak);
        Assert.Equal(Expected, results);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_more_items_than_the_degree_are_in_flight()
    {
        var inFlight = 0;
        var peak = 0;

        _ = await BoundedParallel.Map(_items, async (item, token) =>
        {
            Track(ref inFlight, ref peak, +1);
            await Task.Delay(TimeSpan.FromMilliseconds(10), token);
            Track(ref inFlight, ref peak, -1);
            return item;
        }, degree: 3, TestContext.Current.CancellationToken);

        Assert.InRange(peak, 2, 3);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cancelled_token_stops_the_run()
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var started = 0;

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await BoundedParallel.Map(_items, async (item, token) =>
        {
            if (Interlocked.Increment(ref started) == 2)
            {
                await source.CancelAsync();
            }

            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return item;
        }, degree: 2, source.Token));

        Assert.True(Volatile.Read(ref started) < _items.Length, "the run kept starting items after it was cancelled");
    }

    [Fact, Trait("Lane", "unit")]
    public void The_synchronous_map_keeps_input_order_at_any_degree()
    {
        static string Work(int item) => "r" + item.ToString(CultureInfo.InvariantCulture);

        Assert.Equal(Expected, BoundedParallel.MapSync(_items, Work, degree: 1));
        Assert.Equal(Expected, BoundedParallel.MapSync(_items, Work, degree: 8));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_items_give_no_results_and_one_item_runs_serially()
    {
        Assert.Empty(await BoundedParallel.Map(Array.Empty<int>(), (item, _) => ValueTask.FromResult(item), degree: 8, TestContext.Current.CancellationToken));
        int[] one = [7];
        var single = await BoundedParallel.Map(one, (item, _) => ValueTask.FromResult(item), degree: 8, TestContext.Current.CancellationToken);

        Assert.Equal(one, single);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(1)]
    [InlineData(8)]
    public void The_synchronous_map_throws_the_work_s_own_exception_type_at_any_degree(int degree)
    {
        static int Work(int item) => item == 3 ? throw new InvalidOperationException("item 3") : item;

        var thrown = Assert.Throws<InvalidOperationException>(() => BoundedParallel.MapSync(_items, Work, degree));

        Assert.Equal("item 3", thrown.Message);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(1)]
    [InlineData(8)]
    public async Task The_async_map_throws_the_work_s_own_exception_type_at_any_degree(int degree)
    {
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () => await BoundedParallel.Map(_items, (item, _) => item == 3 ? throw new InvalidOperationException("item 3") : ValueTask.FromResult(item), degree, TestContext.Current.CancellationToken));

        Assert.Equal("item 3", thrown.Message);
    }

    private static void Track(ref int inFlight, ref int peak, int by)
    {
        var now = Interlocked.Add(ref inFlight, by);
        int seen;
        while ((seen = Volatile.Read(ref peak)) < now && Interlocked.CompareExchange(ref peak, now, seen) != seen)
        {
        }
    }
}
