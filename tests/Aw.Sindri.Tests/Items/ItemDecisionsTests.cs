using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Items;

/// <summary>The pure decisions on items (API spec §2.1 step 2, §3.1, §3.6): states, offered actions, closing, session repo scope.</summary>
[Lane(TestLane.Unit)]
public sealed class ItemDecisionsTests
{
    private readonly CommandId _command = new SequentialIdGenerator().New<CommandId>();

    [Theory]
    [InlineData(ItemState.Done, true)]
    [InlineData(ItemState.Cancelled, true)]
    [InlineData(ItemState.New, false)]
    [InlineData(ItemState.Queued, false)]
    [InlineData(ItemState.Running, false)]
    [InlineData(ItemState.NeedsDecision, false)]
    [InlineData(ItemState.NeedsAnswer, false)]
    [InlineData(ItemState.Parked, false)]
    [InlineData(ItemState.TakenOver, false)]
    public void Only_done_and_cancelled_are_closed(ItemState state, bool closed) => Expect.Equal(closed, ItemStates.IsClosed(state));

    [Fact]
    public void Actions_by_state()
    {
        var close = new[] { ActionId.From("CloseItem:done"), ActionId.From("CloseItem:cancelled") };

        foreach (var state in Enum.GetValues<ItemState>())
        {
            var offered = ItemActions.For(state);
            Expect.Sequence(state is ItemState.New or ItemState.Queued or ItemState.NeedsDecision ? close : [], offered);
            Expect.True(ItemActions.Primary(state) is null);
        }
    }

    [Fact]
    public void Close_refuses_an_undefined_reason()
    {
        var item = new ItemRowBuilder().AtVersion(3).Build();

        Expect.Equal(SindriErrors.InvalidInput.Code, ItemDecisions.Close(item, (CloseReason)7, seenVersion: 3, _command).Error.Code);
    }

    [Fact]
    public void Close_at_the_seen_version_changes_state()
    {
        var item = new ItemRowBuilder().AtVersion(3).Build();

        var change = ItemDecisions.Close(item, CloseReason.Done, seenVersion: 3, _command).Value;

        Expect.Equal((item.Id, ItemState.New, ItemState.Done, 4L, _command), (change.Item, change.From, change.To, change.Version, change.CommandId));
        Expect.Equal(ItemState.Cancelled, ItemDecisions.Close(item, CloseReason.Cancelled, 3, _command).Value.To);
    }

    [Fact]
    public void Close_with_a_stale_version_is_SND_ITEM_409()
    {
        var item = new ItemRowBuilder().AtVersion(3).Build();

        var result = ItemDecisions.Close(item, CloseReason.Done, seenVersion: 2, _command);

        Expect.Equal(SindriErrors.ItemConflict.Code, result.Error.Code);
    }

    [Fact]
    public void Closing_a_closed_item_is_SND_ITEM_409()
    {
        var item = new ItemRowBuilder().InState(ItemState.Done, KnownInstants.Epoch).AtVersion(2).Build();

        var result = ItemDecisions.Close(item, CloseReason.Cancelled, seenVersion: 2, _command);

        Expect.Equal(SindriErrors.ItemConflict.Code, result.Error.Code);
        Expect.True(result.Error.Message.Contains("already done", StringComparison.Ordinal));
    }
}
