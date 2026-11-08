using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.BridgeEvents;

/// <summary>
/// The bridge's held window (engine spec §5.4): it covers visibility lag only, so an instance that existed when the event
/// happened but wasn't listed yet gets the event, and an instance started after it (a later attempt) never does.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class HeldItemEventsTests
{
    private static readonly DateTimeOffset _event = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _clock = new(_event);
    private readonly FakeInstances _instances = new();

    [Fact]
    public async Task An_instance_started_before_the_event_but_not_yet_visible_still_gets_it()
    {
        var held = new HeldItemEvents(_instances, _clock);
        var trigger = Item(1);
        held.Hold("T-45", trigger, []);   // delivered while visibility listed nothing
        _instances.Rows = [Row("T-45", 1, _event - TimeSpan.FromSeconds(2))];
        _clock.Advance(TimeSpan.FromSeconds(5));

        var owed = await held.Owed(TestContext.Current.CancellationToken);

        Assert.Equal([(trigger, Address("T-45", 1))], owed.Value);
    }

    [Fact]
    public async Task An_instance_started_after_the_event_does_not_get_it()
    {
        var held = new HeldItemEvents(_instances, _clock);
        held.Hold("T-45", Item(1), []);
        _instances.Rows = [Row("T-45", 2, _event + TimeSpan.FromSeconds(2))];
        _clock.Advance(TimeSpan.FromSeconds(5));

        var owed = await held.Owed(TestContext.Current.CancellationToken);

        Assert.Empty(owed.Value);
    }

    [Fact]
    public async Task An_event_leaves_the_window_after_the_visibility_lag_not_the_event_hold()
    {
        var held = new HeldItemEvents(_instances, _clock);
        held.Hold("T-45", Item(1), []);
        _instances.Rows = [Row("T-45", 1, _event - TimeSpan.FromSeconds(2))];
        _clock.Advance(HeldItemEvents.VisibilityLag + TimeSpan.FromSeconds(1));

        var owed = await held.Owed(TestContext.Current.CancellationToken);

        Assert.Empty(owed.Value);
        Assert.Empty(_instances.Queries);
        Assert.True(HeldItemEvents.VisibilityLag <= TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task One_run_asks_visibility_once_per_item_however_many_events_it_holds()
    {
        var held = new HeldItemEvents(_instances, _clock);
        held.Hold("T-45", Item(1), []);
        held.Hold("T-45", Item(2), []);
        held.Hold("T-46", Item(3), []);
        _instances.Rows = [Row("T-45", 1, _event - TimeSpan.FromSeconds(2)), Row("T-46", 1, _event - TimeSpan.FromSeconds(2))];

        var owed = await held.Owed(TestContext.Current.CancellationToken);

        Assert.Equal(2, _instances.Queries.Count);
        Assert.Equal(3, owed.Value.Count);   // T-45 owes two events to attempt 1, T-46 one
    }

    [Fact]
    public async Task A_delivered_event_is_not_owed_again()
    {
        var held = new HeldItemEvents(_instances, _clock);
        var trigger = Item(1);
        held.Hold("T-45", trigger, []);
        _instances.Rows = [Row("T-45", 1, _event - TimeSpan.FromSeconds(2))];
        _clock.Advance(TimeSpan.FromSeconds(5));
        var first = await held.Owed(TestContext.Current.CancellationToken);

        held.Delivered(trigger, Address("T-45", 1));
        var again = await held.Owed(TestContext.Current.CancellationToken);

        Assert.Equal([(trigger, Address("T-45", 1))], first.Value);
        Assert.Empty(again.Value);
    }

    [Fact]
    public async Task An_instance_started_at_the_event_time_gets_it()
    {
        var held = new HeldItemEvents(_instances, _clock);
        var trigger = Item(1);
        held.Hold("T-45", trigger, []);
        _instances.Rows = [Row("T-45", 1, _event)];   // the boundary: at or before the event counts
        _clock.Advance(TimeSpan.FromSeconds(5));

        var owed = await held.Owed(TestContext.Current.CancellationToken);

        Assert.Equal([(trigger, Address("T-45", 1))], owed.Value);
    }

    private static WaitSatisfied Item(long sequence) =>
        new("service:workflow.events", new EventWaitSource(EventKind.From("item.routed"), sequence), OutcomeName.From("startable"), new Dictionary<FactKey, FactValue>())
        {
            OccurredAt = _event,
        };

    private static InstanceAddress Address(string item, int attempt) => InstanceAddress.Root(ItemKey.From(item), RecipeId.From("workflow.relay"), attempt);

    private static InstanceRow Row(string item, int attempt, DateTimeOffset started) => new(Address(item, attempt).Value, "routed", NeedsYou: false, started);

    /// <summary>Lists <see cref="Rows"/> whose address names the item the query asks for, and records each query.</summary>
    private sealed class FakeInstances : IWorkflowInstances
    {
        public IReadOnlyList<InstanceRow> Rows { get; set; } = [];

        public List<string> Queries { get; } = [];

        public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(InstanceFilter filter, bool ended, CancellationToken cancellationToken) =>
            List(InstanceFilters.ToQuery(filter), ended, cancellationToken);

        public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(string? query, bool ended, CancellationToken cancellationToken)
        {
            Queries.Add(query ?? string.Empty);
            var item = query!.Split('\'')[1]; // why: the bridge's query is "Item = '<item>'"
            IReadOnlyList<InstanceRow> rows = [.. Rows.Where(r => r.Address.Contains(item, StringComparison.Ordinal))];
            return ValueTask.FromResult(Results.Success(rows));
        }
    }
}
