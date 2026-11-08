using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Tests.ListInstances;

/// <summary>The memory of ended instance views a list shares: an ended run is read once, a new run on its address or a running one is read afresh, and past its capacity it forgets one view, not all.</summary>
[Lane(TestLane.Unit)]
public sealed class EndedInstanceViewsTests
{
    private static readonly DateTimeOffset _started = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private static InstanceRow Row(string address, DateTimeOffset? started) => new(address, "done", NeedsYou: false, started);

    [Fact]
    public async Task An_ended_view_is_read_once_per_run_and_a_later_run_on_the_address_is_read_afresh()
    {
        var reader = new CountingReader { Ended = true };
        var views = new EndedInstanceViews(reader);
        var address = InstanceAddress.From("ITM-9/step/1");

        _ = await views.Get(Row(address.Value, _started), address, TestContext.Current.CancellationToken);
        _ = await views.Get(Row(address.Value, _started), address, TestContext.Current.CancellationToken);
        _ = await views.Get(Row(address.Value, _started.AddHours(1)), address, TestContext.Current.CancellationToken);

        Assert.Equal(2, reader.Reads);
    }

    [Fact]
    public async Task A_row_with_no_start_time_is_always_read()
    {
        var reader = new CountingReader { Ended = true };
        var views = new EndedInstanceViews(reader);
        var address = InstanceAddress.From("ITM-9/step/1");

        _ = await views.Get(Row(address.Value, started: null), address, TestContext.Current.CancellationToken);
        _ = await views.Get(Row(address.Value, started: null), address, TestContext.Current.CancellationToken);

        Assert.Equal(2, reader.Reads);
    }

    [Fact]
    public async Task A_running_view_is_never_remembered_so_the_read_after_it_ended_is_the_one_that_sticks()
    {
        var reader = new CountingReader { Ended = false };
        var views = new EndedInstanceViews(reader);
        var address = InstanceAddress.From("ITM-9/step/1");
        var row = Row(address.Value, _started);

        _ = await views.Get(row, address, TestContext.Current.CancellationToken);   // running: not stored
        reader.Ended = true;
        var second = await views.Get(row, address, TestContext.Current.CancellationToken);   // ended: read, stored
        _ = await views.Get(row, address, TestContext.Current.CancellationToken);   // remembered

        Assert.True(second.Value.Outcome is not null);
        Assert.Equal(2, reader.Reads);
    }

    [Fact]
    public async Task A_reader_error_is_returned_and_not_remembered()
    {
        var reader = new CountingReader { Fail = true };
        var views = new EndedInstanceViews(reader);
        var address = InstanceAddress.From("ITM-9/step/1");

        var first = await views.Get(Row(address.Value, _started), address, TestContext.Current.CancellationToken);
        _ = await views.Get(Row(address.Value, _started), address, TestContext.Current.CancellationToken);

        Assert.False(first.IsSuccess);
        Assert.Equal(2, reader.Reads);
    }

    [Fact]
    public async Task Listing_one_more_than_the_capacity_twice_forgets_one_view_at_a_time_not_all_of_them()
    {
        var reader = new CountingReader { Ended = true };
        var views = new EndedInstanceViews(reader);
        var rows = Enumerable.Range(0, EndedInstanceViews.Capacity + 1).Select(static n => Row(string.Create(CultureInfo.InvariantCulture, $"ITM-9/step/{n + 1}"), _started)).ToList();
        foreach (var row in rows)
        {
            _ = await views.Get(row, InstanceAddress.From(row.Address), TestContext.Current.CancellationToken);
        }

        reader.Reads = 0;
        foreach (var row in rows)
        {
            _ = await views.Get(row, InstanceAddress.From(row.Address), TestContext.Current.CancellationToken);
        }

        Assert.InRange(reader.Reads, 1, 3);
    }

    private sealed class CountingReader : IInstanceReader
    {
        public int Reads { get; set; }

        public bool Ended { get; set; }

        public bool Fail { get; set; }

        public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken)
        {
            Reads++;
            if (Fail)
            {
                return ValueTask.FromResult<Result<InstanceView>>(WorkflowErrors.InstanceNotFound.With(address.Value));
            }

            var recipe = new RecipeVersion(RecipeId.From("workflow.step"), 1, RecipeHash.From("sha256:" + new string('a', 64)));
            return ValueTask.FromResult<Result<InstanceView>>(new InstanceView(address, recipe, "done", Detail: null, _started, string.Empty, [], Question: null, Task: null, Run: null, Children: [], Ended ? OutcomeName.From("resolved") : null)
            {
                Id = WorkflowInstanceId.Of(address),
            });
        }
    }
}
