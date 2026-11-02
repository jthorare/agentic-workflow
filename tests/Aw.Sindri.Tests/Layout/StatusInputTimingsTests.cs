using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.Layout;

/// <summary>How long each Step input took to fill, on the status surfaces (#1167): one line per input in declared order, an array in the fields, nothing when the row kept no timings.</summary>
[Lane(TestLane.Unit)]
public sealed class StatusInputTimingsTests
{
    private static readonly InputTiming[] _timings = [new(BlockId.From("input.a"), 412), new(BlockId.From("input.b"), 7)];

    private static InstanceView Timed(params InputTiming[] timings)
    {
        var view = StatusCases.View("desk-task");
        return view with { Task = view.Task! with { Timings = timings } };
    }

    [Fact]
    public void Status_text_lists_each_input_with_its_milliseconds_in_declared_order()
    {
        var text = StatusLayout.Render(Timed(_timings), StatusCases.Now);

        Assert.Contains("  timings   input.a  412 ms\n            input.b  7 ms\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_fields_carry_the_same_timings_and_none_without_them()
    {
        var fields = StatusRecords.Of(Timed(_timings), StatusCases.Now);

        Assert.Equal([new StatusTiming("input.a", 412), new StatusTiming("input.b", 7)], fields.InputTimings);
        var bare = StatusCases.View("desk-task");
        Assert.Null(StatusRecords.Of(bare, StatusCases.Now).InputTimings);
        Assert.DoesNotContain("timings", StatusLayout.Render(bare, StatusCases.Now), StringComparison.Ordinal);
    }

    [Fact]
    public void The_view_takes_the_timings_from_its_context_onto_the_desk_task()
    {
        var view = InstanceViews.Of(StatusCases.Instance("desk-task"), StatusCases.Recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext { Timings = _timings });

        Assert.Equal(_timings, view.Task!.Timings);
    }
}
