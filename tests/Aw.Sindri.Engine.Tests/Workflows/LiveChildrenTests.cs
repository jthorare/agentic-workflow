using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Exceptions;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>Status reads each child live (engine P7 plan, owner answer Q2): the parent's own view lists the children, and the read replaces each one's state with the child's own query; a child it can't reach says so.</summary>
public sealed class LiveChildrenTests
{
    private static readonly RecipeVersion _recipe = new(RecipeId.From("workflow.t-parent"), 1, RecipeHash.From("sha256:" + new string('a', 64)));
    private static readonly InstanceAddress _first = InstanceAddress.From("T-91/t-parent/1/t-child.1");
    private static readonly InstanceAddress _second = InstanceAddress.From("T-91/t-parent/1/t-other.2");

    [Fact, Trait("Lane", "unit")]
    public async Task Each_child_shows_the_state_it_answers_now_not_the_one_the_parent_last_saw()
    {
        var parent = Parent();

        var live = await LiveChildren.Of(parent, (address, _) => Task.FromResult(Answer(address, address == _first ? "parked" : "waiting")), TestContext.Current.CancellationToken);

        Assert.Equal([(_first, "parked"), (_second, "waiting")], live.Children.Select(static c => (c.Address, c.State)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_child_that_cannot_be_reached_says_so_plainly_and_the_others_still_read()
    {
        var parent = Parent();

        var live = await LiveChildren.Of(parent, (address, _) => address == _first
            ? throw new RpcException(RpcException.StatusCode.Unavailable, "worker gone", rawStatus: null)
            : Task.FromResult(Answer(address, "waiting")), TestContext.Current.CancellationToken);

        Assert.Equal([(_first, LiveChildren.Unreachable), (_second, "waiting")], live.Children.Select(static c => (c.Address, c.State)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_view_with_no_children_asks_nobody()
    {
        var parent = Parent() with { Children = [] };

        var live = await LiveChildren.Of(parent, (_, _) => throw new InvalidOperationException("no child to ask"), TestContext.Current.CancellationToken);

        Assert.Same(parent, live);
    }

    private static InstanceView Parent() => InstanceViews.Starting(InstanceAddress.From("T-91/t-parent/1"), _recipe, DateTimeOffset.UnixEpoch, ItemKey.From("T-91"), subject: null) with
    {
        Children = [new ChildView(_first, "running"), new ChildView(_second, "running")],
    };

    private static InstanceView Answer(InstanceAddress address, string state) => InstanceViews.Starting(address, _recipe, DateTimeOffset.UnixEpoch, ItemKey.From("T-91"), subject: null) with { State = state };
}
