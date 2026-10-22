using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Features.GetInstanceEvents;
using Aw.Sindri.Features.StreamEvents;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Host.Endpoints;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.GetInstance;
using Aw.Sindri.Tests.StreamEvents;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.GetInstanceEvents;

/// <summary>
/// 'workflow events' (engine spec §5.6, §6.3; README X16) reads the ledger, never Temporal's history: an instance's
/// events are the rows it mirrored under 'workflow:{address}:…'; a child's and another instance's are left out. The
/// same filter is S1-D6's '?instance=' on the event stream, with '?item='.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class GetInstanceEventsTests
{
    private static readonly InstanceAddress _address = InstanceAddress.From("T-1/step/1");

    [Fact]
    public async Task Events_are_the_instances_own_ledger_rows_with_their_transitions()
    {
        var h = await Ledger();

        var read = (await Service(h).Handle(new GetInstanceEventsRequest(_address.Value), TestContext.Current.CancellationToken)).Value;

        Assert.Equal([2L, 4L], read.Events.Select(static e => e.Sequence));
        Assert.Equal(5, read.Cursor);
        Assert.Equal(new InstanceEventFields("produce", "checks", "step.completed", "done", "1.1"), read.Events[1].Transition);
        var at = read.Events[0].OccurredAt.UtcDateTime.ToString("MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        var actor = read.Events[0].Actor;
        Assert.Equal(
            $"2  {at}  workflow.started  -> produce  start  {actor}\n4  {at}  workflow.transition  produce -> checks  step.completed done  run 1.1  {actor}\n",
            EventsCommand.Lines(read.Events, empty: true));
    }

    [Fact]
    public async Task Reading_an_instance_by_address_touches_only_its_rows()
    {
        var h = await Ledger();
        var counting = new RecordingLedgerReader(h.Store);
        var instances = Instances();

        var read = (await new GetInstanceEventsService(counting, h.Events, instances, instances).Handle(new GetInstanceEventsRequest(_address.Value), TestContext.Current.CancellationToken)).Value;

        Assert.Equal([2L, 4L], read.Events.Select(static e => e.Sequence));
        Assert.Equal((5L, 2), (read.Cursor, counting.RowsRead));
    }

    [Fact]
    public async Task After_the_cursor_there_are_no_events_and_status_says_so()
    {
        var h = await Ledger();

        var read = (await Service(h).Handle(new GetInstanceEventsRequest(_address.Value, After: 5), TestContext.Current.CancellationToken)).Value;

        Assert.Equal((0, 5L, "no events\n"), (read.Events.Count, read.Cursor, EventsCommand.Lines(read.Events, empty: true)));
    }

    [Fact]
    public async Task The_event_stream_filters_by_instance_or_by_item_and_reports_how_far_it_read()
    {
        var h = await Ledger();
        var service = StreamEventsServices.Over(h);
        var ct = TestContext.Current.CancellationToken;

        var byInstance = (await service.Handle(new StreamEventsRequest(0, Instance: _address), ct)).Value;
        var byItem = (await service.Handle(new StreamEventsRequest(0, Item: ItemKey.From("T-1")), ct)).Value;
        var unfiltered = (await service.Handle(new StreamEventsRequest(0), ct)).Value;

        Assert.Equal([2L, 4L], byInstance.Events.Select(static e => e.Sequence));
        Assert.Equal(5L, byInstance.ScannedTo);
        Assert.Equal([2L, 3L, 4L], byItem.Events.Select(static e => e.Sequence));
        Assert.Equal(5, unfiltered.Events.Count);
    }

    [Theory]
    [InlineData("t-1", null, "'t-1' isn't an item id")]
    [InlineData(null, "T-1", "'T-1' isn't an instance address")]
    public void The_event_streams_filter_refuses_a_malformed_item_or_address(string? item, string? instance, string message)
    {
        var filter = EventStreamOpener.Bind(Request(item, instance));

        Assert.Equal("SND-CLI-003", filter.Error.Code);
        Assert.StartsWith(message, filter.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_event_streams_filter_reads_an_item_and_an_address()
    {
        var both = EventStreamOpener.Bind(Request("ENG-142", "T-1/step/1")).Value;

        Assert.Equal((ItemRef.To(ItemAlias.From("ENG-142")), _address), (both.Ref, both.Instance));
    }

    private static Microsoft.AspNetCore.Http.HttpRequest Request(string? item, string? instance)
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.QueryString = new Microsoft.AspNetCore.Http.QueryString(
            "?" + string.Join('&', new[] { item is null ? null : "item=" + item, instance is null ? null : "instance=" + instance }.Where(static q => q is not null)));
        return context.Request;
    }

    [Fact]
    public async Task An_address_no_instance_has_is_SND_WF_001_not_no_events()
    {
        var h = await Ledger();

        var read = await Service(h).Handle(new GetInstanceEventsRequest("T-9/step/1"), TestContext.Current.CancellationToken);

        Assert.Equal("SND-WF-001", read.Error.Code);
    }

    [Fact]
    public async Task A_live_instance_with_no_events_yet_reads_as_none_and_a_later_read_does_not_ask_for_it_again()
    {
        var h = await Ledger();
        var quiet = new OneInstance(StatusCases.View("done") with { Address = InstanceAddress.From("T-3/step/1") });
        var first = await new GetInstanceEventsService(h.Store, h.Events, quiet, quiet).Handle(new GetInstanceEventsRequest("T-3/step/1"), TestContext.Current.CancellationToken);
        var later = await Service(h).Handle(new GetInstanceEventsRequest("T-9/step/1", After: 5), TestContext.Current.CancellationToken);

        Assert.Equal((0, 5L), (first.Value.Events.Count, first.Value.Cursor));
        Assert.Equal((0, 5L), (later.Value.Events.Count, later.Value.Cursor));
    }

    [Fact]
    public async Task An_instance_Temporal_no_longer_knows_still_reads_its_ledger_rows_by_id()
    {
        var h = await Ledger();
        var forgotten = Forgotten();

        var read = await new GetInstanceEventsService(h.Store, h.Events, forgotten, forgotten).Handle(new GetInstanceEventsRequest(WorkflowInstanceId.Of(_address).Value), TestContext.Current.CancellationToken);

        Assert.Equal([2L, 4L], read.Value.Events.Select(static e => e.Sequence));   // not its child's row, not T-2's
    }

    [Fact(Timeout = 30_000)]
    public async Task The_events_command_for_an_address_Temporal_has_dropped_still_prints_its_ledger_rows()
    {
        var h = await Ledger();
        var forgotten = Forgotten();
        var stdout = new StringWriter();
        using var api = ReadApi.Over(forgotten, services => services.AddSingleton<IService<GetInstanceEventsRequest, GetInstanceEventsResponse>>(new GetInstanceEventsService(h.Store, h.Events, forgotten, forgotten)));

        var exit = await EventsCommand.Run(["T-1/step/1"], api.Desk, Services(h), new Output(stdout, new StringWriter(), SliceHarness.Json), TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        Assert.Contains("workflow.transition", stdout.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Temporal's side after retention: it knows one other instance, not <see cref="_address"/>.</summary>
    private static OneInstance Forgotten() => new(StatusCases.View("done") with { Address = InstanceAddress.From("T-9/step/1"), Id = WorkflowInstanceId.Of(InstanceAddress.From("T-9/step/1")) });

    [Fact]
    public async Task A_bare_item_reads_the_root_of_its_latest_attempt()
    {
        var h = await Ledger();

        var read = await Service(h).Handle(new GetInstanceEventsRequest("T-1"), TestContext.Current.CancellationToken);

        Assert.Equal([2L, 4L], read.Value.Events.Select(static e => e.Sequence));
    }

    [Fact]
    public async Task A_block_result_reads_back_as_via_block_and_its_decision_and_prints_that_exact_line()
    {
        var h = await Ledger();
        var payload = new WorkflowEventPayload(WorkflowInstanceId.Of(_address).Value, "route-evidence", "evidence-ui", "step.completed", "ui", "1.1") { Via = "block", Decision = "JD-7" };
        var entry = h.Events.Entry("workflow.transition", payload, "workflow:T-1/step/1:run-a:9", "T-1");
        Assert.False((await h.Store.Append(entry, h.Held.Current!, TestContext.Current.CancellationToken)).IsError);

        var read = (await Service(h).Handle(new GetInstanceEventsRequest(_address.Value), TestContext.Current.CancellationToken)).Value;

        var block = read.Events[^1];
        Assert.Equal(new InstanceEventFields("route-evidence", "evidence-ui", "step.completed", "ui", "1.1", "block", "JD-7"), block.Transition);
        var at = block.OccurredAt.UtcDateTime.ToString("MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(
            $"{block.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)}  {at}  workflow.transition  route-evidence -> evidence-ui  step.completed ui  run 1.1  via block · decision JD-7  {block.Actor}\n",
            EventsCommand.Lines([block], empty: false));
    }

    [Fact]
    public async Task An_agent_report_reads_back_as_via_agent_and_its_tokens_and_prints_that_exact_line()
    {
        var h = await Ledger();
        var payload = new WorkflowEventPayload(WorkflowInstanceId.Of(_address).Value, "produce", "checks", "step.completed", "done", "3.1") { Via = "agent", Tokens = 1_200 };
        Assert.False((await h.Store.Append(h.Events.Entry("workflow.transition", payload, "workflow:T-1/step/1:run-a:10", "T-1"), h.Held.Current!, TestContext.Current.CancellationToken)).IsError);

        var read = (await Service(h).Handle(new GetInstanceEventsRequest(_address.Value), TestContext.Current.CancellationToken)).Value;

        var agent = read.Events[^1];
        Assert.Equal(("agent", 1_200), (agent.Transition.Via, agent.Transition.Tokens));
        var at = agent.OccurredAt.UtcDateTime.ToString("MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(
            $"{agent.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)}  {at}  workflow.transition  produce -> checks  step.completed done  run 3.1  via agent · tokens 1200  {agent.Actor}\n",
            EventsCommand.Lines([agent], empty: false));
    }

    [Fact]
    public async Task A_failed_step_serves_its_reason_and_no_other_event_does()
    {
        var h = await Ledger();
        var failed = new StepFailed("agent:session", RunToken.From("4.1"), "lost", Tokens: 0, new string('a', 64));
        var detail = WorkflowEventDetail.Of(failed, WorkflowInstanceId.Of(_address), suspension: null) with { Reason = "lost" };
        var payload = new WorkflowEventPayload(WorkflowInstanceId.Of(_address).Value, "write", "parked", "step.failed", Outcome: null, "4.1", detail);
        Assert.False((await h.Store.Append(h.Events.Entry("workflow.transition", payload, "workflow:T-1/step/1:run-a:11", "T-1"), h.Held.Current!, TestContext.Current.CancellationToken)).IsError);

        var read = (await Service(h).Handle(new GetInstanceEventsRequest(_address.Value), TestContext.Current.CancellationToken)).Value;

        Assert.Equal("lost", read.Events[^1].Transition.Reason);
        Assert.All(read.Events.Take(read.Events.Count - 1), e => Assert.Null(e.Transition.Reason));
        Assert.DoesNotContain("reason", System.Text.Json.JsonSerializer.Serialize(read.Events[1].Transition, SliceHarness.Json), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"reason\":\"lost\"", System.Text.Json.JsonSerializer.Serialize(read.Events[^1].Transition, SliceHarness.Json), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_events_response_with_a_block_and_a_desk_event_matches_its_golden()
    {
        var h = await Ledger();
        var payload = new WorkflowEventPayload(WorkflowInstanceId.Of(_address).Value, "route-evidence", "evidence-ui", "step.completed", "ui", "1.1") { Via = "block", Decision = "JD-7" };
        Assert.False((await h.Store.Append(h.Events.Entry("workflow.transition", payload, "workflow:T-1/step/1:run-a:9", "T-1"), h.Held.Current!, TestContext.Current.CancellationToken)).IsError);

        var read = (await Service(h).Handle(new GetInstanceEventsRequest(_address.Value), TestContext.Current.CancellationToken)).Value;

        Golden.Match(ApiGolden.Reindent(System.Text.Json.JsonSerializer.Serialize(read, SliceHarness.Json)), typeof(GetInstanceEventsTests).Assembly, "Golden/Events/block-and-desk.json", new Normalization());
    }

    [Fact]
    public async Task A_desk_event_leaves_via_and_decision_out_of_its_json()
    {
        var h = await Ledger();

        var read = (await Service(h).Handle(new GetInstanceEventsRequest(_address.Value), TestContext.Current.CancellationToken)).Value;

        var json = System.Text.Json.JsonSerializer.Serialize(read.Events[1].Transition, SliceHarness.Json);
        Assert.DoesNotContain("via", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("decision", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Timeout = 30_000)]
    public async Task Following_ends_with_exit_0_when_it_is_interrupted()
    {
        var h = await Ledger();
        using var interrupt = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var stdout = new CancelOnWrite(interrupt);

        using var api = Api(h);

        var exit = await EventsCommand.Run(["T-1/step/1", "--follow"], api.Desk, Services(h), new Output(stdout, new StringWriter(), SliceHarness.Json), interrupt.Token);

        Assert.Equal(0, exit);
        Assert.Contains("workflow.started", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task Following_prints_an_event_appended_after_it_started_exactly_once()
    {
        var h = await Ledger();
        using var interrupt = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var stdout = new SignalOnWrite();
        using var api = Api(h);
        var run = EventsCommand.Run(["T-1/step/1", "--follow"], api.Desk, Services(h), new Output(stdout, new StringWriter(), SliceHarness.Json), interrupt.Token);
        await stdout.Wait(2);   // the two rows it read first, one write each

        var entry = h.Events.Entry("workflow.transition", new InstanceEventFields("checks", "done", "step.completed", "done", "1.2"), "workflow:T-1/step/1:run-a:5") with { Item = "T-1" };
        Assert.False((await h.Store.Append(entry, h.Held.Current!, TestContext.Current.CancellationToken)).IsError);
        var cursor = (await h.Store.Head(TestContext.Current.CancellationToken)).Value;   // the appended row's sequence: the seeded rows plus one
        var fromCursor = $"events?after={cursor.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        while (!await stdout.Wait(3, TimeSpan.FromMilliseconds(50)))
        {
            h.Clock.Advance(EventsCommand.Poll);   // the poll's delay may not have started yet: advance until it has read the row
        }

        while (!api.Calls.Where(c => c.EndsWith(fromCursor, StringComparison.Ordinal)).Skip(1).Any())
        {
            h.Clock.Advance(EventsCommand.Poll);   // polls after the row: two must have read from its cursor, and none may print it again
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        await interrupt.CancelAsync();
        var exit = await run.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        var lines = stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        _ = Assert.Single(lines, static l => l.Contains("checks -> done", StringComparison.Ordinal));
    }

    [Fact(Timeout = 30_000)]
    public async Task Following_ends_with_exit_0_when_stdout_is_closed()
    {
        var h = await Ledger();

        using var api = Api(h);

        var exit = await EventsCommand.Run(["T-1/step/1", "--follow", "--json"], api.Desk, Services(h), new Output(new ClosedPipe(), new StringWriter(), SliceHarness.Json), TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
    }

    [Theory(Timeout = 30_000)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_events_command_on_an_unknown_instance_is_SND_WF_001_with_exit_2_and_never_follows(bool follow)
    {
        var h = await Ledger();
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        using var api = Api(h);

        var exit = await EventsCommand.Run(follow ? ["T-9/step/1", "--follow"] : ["T-9/step/1"], api.Desk, Services(h), new Output(stdout, stderr, SliceHarness.Json), TestContext.Current.CancellationToken);

        Assert.Equal((2, string.Empty), (exit, stdout.ToString()));
        Assert.StartsWith("SND-WF-001 ", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_following_verbs_listen_for_an_interrupt()
    {
        Assert.True(Interrupt.Streams(["workflow", "events", "T-1", "--follow"]));
        Assert.True(Interrupt.Streams(["status", "--follow"]));
        Assert.True(Interrupt.Streams(["status", "--follow", "--json"]));
        Assert.False(Interrupt.Streams(["workflow", "events", "T-1"]));
        Assert.False(Interrupt.Streams(["status"]));
        Assert.False(Interrupt.Streams(["workflow", "status", "T-1", "--follow"]));
    }

    private static GetInstanceEventsService Service(SliceHarness h)
    {
        var instances = Instances();
        return new GetInstanceEventsService(h.Store, h.Events, instances, instances);
    }

    private static OneInstance Instances() => new(StatusCases.View("done") with { Address = _address, Id = WorkflowInstanceId.Of(_address) });

    /// <summary>The events and instance routes over this ledger and the one instance <see cref="Instances"/> holds.</summary>
    private static ReadApi Api(SliceHarness h) => ReadApi.Over(Instances(), services => services.AddSingleton<IService<GetInstanceEventsRequest, GetInstanceEventsResponse>>(Service(h)));

    private static ServiceProvider Services(SliceHarness h) => new ServiceCollection()
        .AddSingleton<TimeProvider>(h.Clock)
        .BuildServiceProvider();

    /// <summary>Five rows: a heartbeat, T-1/step/1's start, its child's start, its transition, and T-2/step/1's start.</summary>
    private static async Task<SliceHarness> Ledger()
    {
        var h = new SliceHarness();
        var grant = await h.HoldLease();
        var ct = TestContext.Current.CancellationToken;
        (string Type, string Key, string? Item, InstanceEventFields Fields)[] rows =
        [
            ("service.heartbeat", "service.heartbeat:1:1", null, InstanceEventFields.None),
            ("workflow.started", "workflow:T-1/step/1:run-a:1", "T-1", new(string.Empty, "produce", "start")),
            ("workflow.started", "workflow:T-1/step/1/step.2:run-b:1", "T-1", new(string.Empty, "produce", "start")),
            ("workflow.transition", "workflow:T-1/step/1:run-a:3", "T-1", new("produce", "checks", "step.completed", "done", "1.1")),
            ("workflow.started", "workflow:T-2/step/1:run-c:1", "T-2", new(string.Empty, "produce", "start")),
        ];
        foreach (var (type, key, item, fields) in rows)
        {
            var entry = h.Events.Entry(type, fields, key) with { Item = item };
            Assert.False((await h.Store.Append(entry, grant, ct)).IsError);
        }

        return h;
    }
}
