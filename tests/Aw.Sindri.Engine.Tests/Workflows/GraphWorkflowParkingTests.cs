using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Client;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The parking blocks on the time-skipping server (issue 2258, PR 9): a 'wait' node sleeps on a timer and the run goes on, an 'ask_owner' node takes the default branch at its timeout, and an answer
/// sent through the 'Answer' update before the timeout wins. The server skips time while the test awaits the run's result.
/// </summary>
// why: quarantined in the heavy lane (no gate runs it) until issue 2574 lands: a run left open by an earlier test stops time on the shared server, and the later tests time out
[Lane(TestLane.Heavy)]
public sealed partial class GraphWorkflowParkingTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonObject Ask(string timeout = "1h") => new()
    {
        ["question"] = "Ship the release?",
        ["choices"] = new JsonArray("yes", "no"),
        ["timeout"] = timeout,
        ["default"] = "no",
    };

    private static GraphSpec AskSpec(string timeout = "1h") => GraphRig.Spec("unlimited",
        [GraphRig.Node("start", "manual"), GraphRig.Node("ask", "ask_owner", config: Ask(timeout)), GraphRig.Node("yes", "echo"), GraphRig.Node("no", "echo")],
        [GraphRig.Edge("start", "ask"), GraphRig.Edge("ask", "yes", "answered"), GraphRig.Edge("ask", "no", "timeout")]);

    // why: a bounded wait, so a run that never ends fails the test with a stack instead of hanging the lane (the PR9b gate hung 45 min)
    private static async Task<GraphWorkflowResult> Result(GraphRig rig, WorkflowRunId run)
    {
        using var limit = Limit();
        return await rig.Client.GetWorkflowHandle(run.Value).GetResultAsync<GraphWorkflowResult>(rpcOptions: new RpcOptions { CancellationToken = limit.Token });
    }

    // why: the server did not skip time on its own while GetResultAsync waited (both timer tests hung), so the test skips it; again until the run ends, because the timer may start after the first skip
    private async Task SkipUntilEnded(GraphRig rig, WorkflowRunId run, TimeSpan step)
    {
        using var limit = Limit();
        while ((await rig.Runs.Get(run, limit.Token)).Value.Status is not (WorkflowRunStatus.Succeeded or WorkflowRunStatus.Failed or WorkflowRunStatus.Cancelled))
        {
            await server.Environment.DelayAsync(step);
            await Task.Delay(TimeSpan.FromMilliseconds(100), limit.Token);
        }
    }

    private static CancellationTokenSource Limit()
    {
        var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(TimeSpan.FromSeconds(30));
        return limit;
    }

    // why: the limit cancels the update call itself, not only the delay between tries
    private static Task<string> Answer(WorkflowHandle handle, GraphAnswerInput answer, CancellationToken limit) =>
        handle.ExecuteUpdateAsync<string>("Answer", [answer], new WorkflowUpdateOptions { Rpc = new RpcOptions { CancellationToken = limit } });

    [Fact]
    public async Task A_wait_node_sleeps_on_a_timer_and_the_run_then_goes_on()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("hold", "wait", config: new JsonObject { ["duration"] = "15m" }), GraphRig.Node("after", "echo")],
            [GraphRig.Edge("start", "hold"), GraphRig.Edge("hold", "after")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        await WaitUntilRunning(rig, started.RunId, "hold");   // the node parks before any time is skipped
        await SkipUntilEnded(rig, started.RunId, TimeSpan.FromMinutes(16));
        var result = await Result(rig, started.RunId);
        var run = await rig.Final(started.RunId);
        var history = await rig.Client.GetWorkflowHandle(started.RunId.Value).FetchHistoryAsync();

        Assert.Equal(WorkflowRunStatus.Succeeded, result.Status);
        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        // why: a Sleep that did nothing would also end Succeeded; only a 15 minute timer in the history and a 15 minute gap between its first and last event show the node slept
        Assert.Contains(history.Events, static e => e.TimerStartedEventAttributes != null && e.TimerStartedEventAttributes.StartToFireTimeout.ToTimeSpan() == TimeSpan.FromMinutes(15));
        Assert.True(history.Events.Last().EventTime.ToDateTime() - history.Events.First().EventTime.ToDateTime() >= TimeSpan.FromMinutes(15));
        Assert.Equal([$"{rig.Slug}@1:1/after"], rig.Blocks.Log);
        Assert.All(await rig.NodeStatuses(started.RunId), static s => Assert.Equal(NodeRunStatus.Succeeded, s.Value));
    }

    [Fact]
    public async Task A_cancel_ends_a_wait_node_cancelled_and_skips_the_rest()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("hold", "wait", config: new JsonObject { ["duration"] = "1d" }), GraphRig.Node("after", "echo")],
            [GraphRig.Edge("start", "hold"), GraphRig.Edge("hold", "after")]));
        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        await WaitUntilRunning(rig, started.RunId, "hold");

        await rig.Client.GetWorkflowHandle(started.RunId.Value).CancelAsync();
        var run = await rig.Final(started.RunId);
        var nodes = await rig.NodeStatuses(started.RunId);

        Assert.Equal(WorkflowRunStatus.Cancelled, run.Status);
        Assert.Equal(NodeRunStatus.Cancelled, nodes["hold"]);
        Assert.Equal(NodeRunStatus.Skipped, nodes["after"]);
        Assert.Empty(rig.Blocks.Log);
    }

    [Fact]
    public async Task An_unanswered_question_takes_the_timeout_branch_with_the_default()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(AskSpec());

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        await SkipUntilEnded(rig, started.RunId, TimeSpan.FromMinutes(61));
        var result = await Result(rig, started.RunId);
        var nodes = await rig.NodeStatuses(started.RunId);
        var ask = (await rig.Runs.NodeRuns(started.RunId, Token)).Value.Single(static n => n.NodeId.Value == "ask");

        Assert.Equal(WorkflowRunStatus.Succeeded, result.Status);
        Assert.Equal([$"{rig.Slug}@1:1/no"], rig.Blocks.Log);
        Assert.Equal(NodeRunStatus.Skipped, nodes["yes"]);
        Assert.Equal([$"{rig.Slug}@1:1/ask"], rig.Questions.Asked);
        var output = JsonNode.Parse(ask.Output!)!;
        Assert.Equal(("no", "timeout"), (output["answer"]!.GetValue<string>(), output["answeredBy"]!.GetValue<string>()));
    }

    [Fact]
    public async Task An_answer_before_the_timeout_wins_and_is_attributed_to_the_session_that_gave_it()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(AskSpec());
        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);

        await AnswerWhenWaiting(rig, started.RunId, new GraphAnswerInput("ask", Text: null, Choice: "yes", "session:abc123"));
        var result = await Result(rig, started.RunId);
        var nodes = await rig.NodeStatuses(started.RunId);
        var ask = (await rig.Runs.NodeRuns(started.RunId, Token)).Value.Single(static n => n.NodeId.Value == "ask");

        Assert.Equal(WorkflowRunStatus.Succeeded, result.Status);
        Assert.Equal([$"{rig.Slug}@1:1/yes"], rig.Blocks.Log);
        Assert.Equal(NodeRunStatus.Skipped, nodes["no"]);
        var output = JsonNode.Parse(ask.Output!)!;
        Assert.Equal(("yes", "session:abc123"), (output["answer"]!.GetValue<string>(), output["answeredBy"]!.GetValue<string>()));
    }

    [Fact]
    public async Task An_answer_that_is_not_a_choice_and_an_answer_to_a_node_that_does_not_wait_are_refused()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(AskSpec());
        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        var handle = rig.Client.GetWorkflowHandle(started.RunId.Value);
        using var limit = Limit();
        Temporalio.Exceptions.WorkflowUpdateFailedException? notAChoice = null;
        while (notAChoice is null)
        {
            try
            {
                _ = await Answer(handle, new GraphAnswerInput("ask", "maybe", Choice: null, "owner"), limit.Token);
            }
            catch (Temporalio.Exceptions.WorkflowUpdateFailedException e) when (e.InnerException!.Message.Contains("not one of the choices", StringComparison.Ordinal))
            {
                notAChoice = e;   // the node waits now: it refused the answer for what it is, not because it was not waiting yet
            }
            catch (Temporalio.Exceptions.WorkflowUpdateFailedException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), limit.Token);
            }
        }

        var wrongNode = await Assert.ThrowsAsync<Temporalio.Exceptions.WorkflowUpdateFailedException>(async () =>
            await Answer(handle, new GraphAnswerInput("start", "yes", Choice: null, "owner"), limit.Token));

        Assert.Contains("not one of the choices", notAChoice.InnerException!.Message, StringComparison.Ordinal);
        Assert.Contains("not waiting", wrongNode.InnerException!.Message, StringComparison.Ordinal);
        _ = await Answer(handle, new GraphAnswerInput("ask", "no", Choice: null, "owner"), limit.Token);   // the node still waits, and a real answer ends it
        Assert.Equal(WorkflowRunStatus.Succeeded, (await Result(rig, started.RunId)).Status);
    }

    // why: the question is posted by the block's activity, a moment before the workflow starts to wait; an answer sent in between is refused, so the test retries
    private static async Task AnswerWhenWaiting(GraphRig rig, WorkflowRunId run, GraphAnswerInput answer)
    {
        using var limit = Limit();
        var handle = rig.Client.GetWorkflowHandle(run.Value);
        while (true)
        {
            try
            {
                _ = await Answer(handle, answer, limit.Token);
                return;
            }
            catch (Temporalio.Exceptions.WorkflowUpdateFailedException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), limit.Token);
            }
        }
    }

    private static async Task WaitUntilRunning(GraphRig rig, WorkflowRunId run, string node)
    {
        using var limit = Limit();
        while (!(await rig.NodeStatuses(run)).TryGetValue(node, out var status) || status != NodeRunStatus.Running)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), limit.Token);
        }
    }
}
