using Aw.Kernel.Testing;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Home;

/// <summary>The one rule a Home collector writes by (home spec §3.1, §3.2): raise what is new, clear what stopped holding, change only on the why chip, waits-on, actions or links.</summary>
[Lane(TestLane.Unit)]
public sealed class HomeReconcileTests
{
    private const string A = "lane:recycle:meta";
    private const string B = "lane:recycle:ideas";
    private const string C = "lane:recycle:qa";

    private static HomeRaise Want(string id, string why = "at 210k", string waitsOn = "you") =>
        new(HomeRowSamples.Row(id) with { Facts = [new HomeFact(HomeFactKind.Why, why), new HomeFact(HomeFactKind.Object, id), new HomeFact(HomeFactKind.From, "lane")], WaitsOn = waitsOn }, Edges: []);

    [Fact]
    public void A_new_row_is_raised_a_gone_row_is_cleared_and_a_quiet_one_writes_nothing()
    {
        HomeRow[] held = [Want(A).Row with { Version = 3 }, Want(B).Row];

        var plan = HomeReconcile.Plan([Want(A), Want(C)], held);

        Assert.Equal(C, Assert.Single(plan.Raised).Row.Id.Value);
        Assert.Empty(plan.Changed);
        Assert.Equal(B, Assert.Single(plan.Cleared).Value);
        Assert.False(plan.IsEmpty);
    }

    [Fact]
    public void The_same_rows_on_both_sides_make_an_empty_plan_even_when_the_title_and_age_moved()
    {
        var held = Want(A).Row with { Title = "meta: at 200k, recycle from a digest", Since = HomeRowSamples.Since.AddHours(-2) };

        var plan = HomeReconcile.Plan([Want(A)], [held]);

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void A_moved_why_chip_changes_the_row_at_the_next_version_and_keeps_its_owner_state()
    {
        var held = Want(A, why: "at 210k").Row with { Version = 4, State = HomeState.Snoozed, SnoozeUntil = HomeRowSamples.Since.AddHours(3), Title = "meta: at 210k" };
        var next = Want(A, why: "at 250k");
        next = next with { Row = next.Row with { Title = "meta: at 250k" } };

        var plan = HomeReconcile.Plan([next], [held]);

        var changed = Assert.Single(plan.Changed).Row;
        Assert.Equal(5, changed.Version);
        Assert.Equal("at 250k", changed.Facts[0].Text);
        Assert.Equal("meta: at 250k", changed.Title);
        Assert.Equal(HomeState.Snoozed, changed.State);
        Assert.Equal(held.SnoozeUntil, changed.SnoozeUntil);
        Assert.Empty(plan.Raised);
        Assert.Empty(plan.Cleared);
    }

    [Fact]
    public void A_moved_waits_on_or_action_list_changes_the_row()
    {
        var held = Want(A).Row;
        var waits = Want(A, waitsOn: "meta is merging (queue position 2)");
        var actions = Want(B);
        actions = actions with { Row = actions.Row with { Actions = ["open"] } };

        var plan = HomeReconcile.Plan([waits, actions], [held, Want(B).Row]);

        Assert.Equal([A, B], plan.Changed.Select(static c => c.Row.Id.Value).ToArray());
    }

    [Fact]
    public void A_source_that_lists_one_object_twice_raises_one_row()
    {
        var plan = HomeReconcile.Plan([Want(A), Want(A, why: "at 300k")], []);

        Assert.Equal("at 210k", Assert.Single(plan.Raised).Row.Facts[0].Text);
    }

    [Fact]
    public void A_done_row_whose_trigger_stopped_holding_is_cleared_too()
    {
        var plan = HomeReconcile.Plan([], [Want(A).Row with { State = HomeState.Done, DoneAt = HomeRowSamples.Since }]);

        Assert.Equal(A, Assert.Single(plan.Cleared).Value);
    }
}
