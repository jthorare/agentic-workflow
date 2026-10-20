using System.Net;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Tests.ApiGoldens;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Server;

/// <summary>
/// GET /sindri/events?types=workflow.instance-changed through the real endpoint: a stream of only the derived type sends no "id:" line,
/// so with no explicit cursor it opens at the ledger head and never replays the rows that were there; an explicit cursor is honored.
/// </summary>
public sealed class EventStreamOpenHeadTests
{
    private const string Route = "/sindri/events?types=workflow.instance-changed";
    private static readonly string _item = "ITM-" + new string('a', 32);

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_derived_only_stream_with_no_cursor_replays_nothing_and_sends_one_frame_for_a_row_after_the_open()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct);
        await Append(host, "T-1", "seeded-1", ct);
        await Append(host, "T-2", "seeded-2", ct);

        await using var stream = await EventStreamClient.Open(host, Route);
        await Append(host, "T-3", "after-open", ct);
        await Append(host, "T-4", "sentinel", ct);

        Assert.Equal("event: workflow.instance-changed", await stream.Line());
        Assert.Contains("\"instance\":\"" + _item + "/T-3\"", await stream.Line(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, await stream.Line());
        Assert.Equal("event: workflow.instance-changed", await stream.Line());   // the next frame is the sentinel's: the first row gave exactly one
        Assert.Contains("\"instance\":\"" + _item + "/T-4\"", await stream.Line(), StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_derived_only_stream_with_after_zero_asks_for_the_history_and_gets_it()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct);
        await Append(host, "T-1", "seeded-1", ct);

        await using var stream = await EventStreamClient.Open(host, Route + "&after=0");

        Assert.Equal("event: workflow.instance-changed", await stream.Line());
        Assert.Contains("\"instance\":\"" + _item + "/T-1\"", await stream.Line(), StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_bad_after_on_a_derived_only_stream_is_a_400_before_any_frame()
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);

        var answer = await host.Http.Ask(Get(Route + "&after=nope", Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
    }

    private static Task Append(GoldenHost host, string instance, string key, CancellationToken ct) =>
        ItemScenarios.AppendFor(host, _item, EventTypes.WorkflowTransition,
            new WorkflowEventPayload(_item + "/" + instance, "produce", "checks", "step.completed", Outcome: null, Run: null), "workflow:" + key, ct);
}
