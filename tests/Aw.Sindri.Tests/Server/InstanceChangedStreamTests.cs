using System.Text;
using System.Text.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Features.StreamEvents;
using Aw.Sindri.Host.Endpoints;
using Aw.Sindri.Tests.StreamEvents;

namespace Aw.Sindri.Tests.Server;

/// <summary>
/// 'workflow.instance-changed' (API spec §5): a frame a client asks for by name, one per instance whose served view changed in a
/// page of the ledger, derived from the rows the instance mirrored. It is never stored, so a view rebuild can't repeat it.
/// </summary>
public sealed class InstanceChangedStreamTests : IDisposable
{
    private const string Wanted = "workflow.instance-changed";

    private readonly SliceHarness _h = new();
    private readonly MemoryStream _body = new();
    private string _next => (++_appended).ToString(System.Globalization.CultureInfo.InvariantCulture);
    private int _appended;

    public void Dispose() => _body.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task One_instance_change_is_exactly_one_frame_with_its_address_state_and_cursor()
    {
        await Append("workflow.transition", Row("T-1/step/1", "produce", "checks"));

        var frames = await Stream(new StreamEventsRequest(0, Types: Types(Wanted)));

        Assert.Equal(["event: workflow.instance-changed\ndata: {\"instance\":\"T-1/step/1\",\"state\":\"checks\",\"cursor\":1}\n\n"], frames);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_change_of_another_instance_gives_none_to_a_stream_for_this_one()
    {
        await Append("workflow.transition", Row("T-2/step/1", "produce", "checks"));
        await Append("workflow.transition", Row("T-1/step/1/step.2", "produce", "checks"));   // its child

        var frames = await Stream(new StreamEventsRequest(0, Instance: InstanceAddress.From("T-1/step/1"), Types: Types(Wanted)));

        Assert.Empty(frames);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_burst_is_one_frame_per_instance_at_its_newest_cursor()
    {
        await Append("workflow.started", Row("T-1/step/1", "", "produce"));
        await Append("workflow.transition", Row("T-2/step/1", "produce", "checks"));
        await Append("workflow.transition", Row("T-1/step/1", "produce", "checks"));
        await Append("workflow.transition", Row("T-1/step/1", "checks", "done"));

        var frames = await Stream(new StreamEventsRequest(0, Types: Types(Wanted)));

        Assert.Equal(
            [
                "event: workflow.instance-changed\ndata: {\"instance\":\"T-2/step/1\",\"state\":\"checks\",\"cursor\":2}\n\n",
                "event: workflow.instance-changed\ndata: {\"instance\":\"T-1/step/1\",\"state\":\"done\",\"cursor\":4}\n\n",
            ],
            frames);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Rows_that_change_no_instance_view_and_a_views_rebuild_give_none()
    {
        await Append("service.heartbeat", Row("none", "a", "b"));
        await Append("views.rebuilt", Row("none", "a", "b"));
        await Append("workflow.injection", Row("T-1/step/1", "produce", "produce"));

        var frames = await Stream(new StreamEventsRequest(0, Types: Types(Wanted)));

        Assert.Empty(frames);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stream_that_does_not_name_it_never_gets_it()
    {
        await Append("workflow.transition", Row("T-1/step/1", "produce", "checks"));

        var named = await Stream(new StreamEventsRequest(0, Types: Types("workflow.*")));
        var every = await Stream(new StreamEventsRequest(0));

        Assert.All(named.Concat(every), f => Assert.DoesNotContain(Wanted, f, StringComparison.Ordinal));
        Assert.Equal(2, named.Count + every.Count);   // each still carries the raw row
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Asked_for_with_other_types_the_raw_workflow_row_is_not_sent_but_the_other_types_are()
    {
        await Append("service.heartbeat", Row("none", "a", "b"));
        await Append("workflow.transition", Row("T-1/step/1", "produce", "checks"));

        var frames = await Stream(new StreamEventsRequest(0, Types: Types("service.heartbeat," + Wanted)));

        Assert.Equal(2, frames.Count);
        Assert.StartsWith("event: service.heartbeat\nid: 1\n", frames[0], StringComparison.Ordinal);
        Assert.StartsWith("event: workflow.instance-changed\n", frames[1], StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_same_instance_gives_one_frame_and_its_child_gives_none_to_the_parents_stream()
    {
        await Append("workflow.transition", Row("T-1/step/1/step.2", "produce", "checks"));   // its child
        await Append("workflow.transition", Row("T-1/step/1", "produce", "checks"));

        var frames = await Stream(new StreamEventsRequest(0, Instance: InstanceAddress.From("T-1/step/1"), Types: Types(Wanted)));

        Assert.Equal(["event: workflow.instance-changed\ndata: {\"instance\":\"T-1/step/1\",\"state\":\"checks\",\"cursor\":2}\n\n"], frames);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_the_raw_workflow_glob_the_rows_and_the_derived_frame_both_go_out()
    {
        await Append("workflow.started", Row("T-1/step/1", "", "produce"));
        await Append("workflow.transition", Row("T-1/step/1", "produce", "checks"));

        var frames = await Stream(new StreamEventsRequest(0, Types: Types("workflow.*," + Wanted)));

        Assert.Equal(
            ["event: workflow.started\n", "event: workflow.transition\n", "event: workflow.instance-changed\n"],
            frames.Select(static f => f[..(f.IndexOf('\n', StringComparison.Ordinal) + 1)]), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_only_the_transition_glob_the_started_row_gives_no_raw_frame_but_still_the_derived_one()
    {
        await Append("workflow.started", Row("T-1/step/1", "", "produce"));

        var frames = await Stream(new StreamEventsRequest(0, Types: Types("workflow.transition," + Wanted)));

        Assert.Equal(["event: workflow.instance-changed\ndata: {\"instance\":\"T-1/step/1\",\"state\":\"produce\",\"cursor\":1}\n\n"], frames);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_burst_split_over_two_pages_gives_a_frame_per_page()
    {
        await Append("workflow.transition", Row("T-1/step/1", "a", "b"));
        await Append("workflow.transition", Row("T-1/step/1", "b", "c"));
        await Append("workflow.transition", Row("T-1/step/1", "c", "d"));

        var frames = await Stream(new StreamEventsRequest(0, Max: 2, Types: Types(Wanted)));

        Assert.Equal(
            [
                "event: workflow.instance-changed\ndata: {\"instance\":\"T-1/step/1\",\"state\":\"c\",\"cursor\":2}\n\n",
                "event: workflow.instance-changed\ndata: {\"instance\":\"T-1/step/1\",\"state\":\"d\",\"cursor\":3}\n\n",
            ],
            frames);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_change_a_row_records_is_read_from_its_public_payload_only()
    {
        var row = Public("workflow.transition", """{"instance":"T-1/step/1","from":"a","to":"b","trigger":"answer","detail":{"reason":"free text"}}""", 7);

        Assert.Equal(new InstanceChanged("T-1/step/1", "b", 7), InstanceChanged.From(row));
        Assert.Null(InstanceChanged.From(Public("workflow.transition", "null", 8)));
        Assert.Null(InstanceChanged.From(Public("service.heartbeat", """{"instance":"T-1/step/1","to":"b"}""", 9)));
    }

    private static TypeFilter Types(string text) => TypeFilter.Parse(text).Value;

    private static WorkflowEventPayload Row(string instance, string from, string to) => new(instance, from, to, "step.completed", Outcome: null, Run: null);

    private PublicEvent Public(string type, string payload, long sequence) => new(sequence, EventId.From(new Guid(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), type, 1, _h.Clock.GetUtcNow(), "service:host", "local", 0,
        Item: null, CorrelationId.From(new Guid(2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), CausationId: null, TraceId: null, JsonDocument.Parse(payload).RootElement);

    private async Task Append<T>(string type, T payload)
    {
        var grant = _h.Held.Current ?? await _h.HoldLease();
        var instance = payload is WorkflowEventPayload p ? p.Instance : "none";
        var key = type.StartsWith("workflow.", StringComparison.Ordinal) ? $"workflow:{instance}:run:{_next}" : $"{type}:{_next}";
        var entry = _h.Events.Entry(type, payload, key);
        Assert.False((await _h.Store.Append(entry, grant, TestContext.Current.CancellationToken)).IsError);
    }

    /// <summary>Runs the stream until it has read the whole ledger and waits for more; returns the frames it wrote, one string each.</summary>
    private async Task<List<string>> Stream(StreamEventsRequest request)
    {
        _body.SetLength(0);
        var signal = new RecordingSignal(_h.Clock);
        var pump = new EventPump(_h.Store, signal, _h.Clock, SliceHarness.Json, new ListLogger<EventPump>());
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var running = pump.Run(new SseWriter(_body), StreamEventsServices.Over(_h), request, stop.Token);

        await signal.Entered(1);
        await stop.CancelAsync();
        await running;

        return [.. Encoding.UTF8.GetString(_body.ToArray()).Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Select(static f => f + "\n\n")];
    }
}
