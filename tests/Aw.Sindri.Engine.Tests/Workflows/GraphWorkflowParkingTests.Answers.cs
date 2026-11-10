using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Tests.Workflows;

// The cancel, double-answer, late-answer and answer-at-the-timeout cases of the 'ask_owner' node (issue 2258, PR 9).
public sealed partial class GraphWorkflowParkingTests
{
    // why: the run must stay alive after the question ends, so a later answer is refused for the node, not because the run is over; 'held' is the branch that leads to a 1 day wait
    private static GraphSpec HoldSpec(string held) => GraphRig.Spec("unlimited",
        [GraphRig.Node("start", "manual"), GraphRig.Node("ask", "ask_owner", config: Ask()), GraphRig.Node("hold", "wait", config: new JsonObject { ["duration"] = "1d" }), GraphRig.Node("other", "echo")],
        [GraphRig.Edge("start", "ask"), GraphRig.Edge("ask", "hold", held), GraphRig.Edge("ask", "other", held == "answered" ? "timeout" : "answered")]);

    private static async Task<(string Answer, string By)> AskOutput(GraphRig rig, WorkflowRunId run)
    {
        var ask = (await rig.Runs.NodeRuns(run, Token)).Value.Single(static n => n.NodeId.Value == "ask");
        var output = JsonNode.Parse(ask.Output!)!;
        return (output["answer"]!.GetValue<string>(), output["answeredBy"]!.GetValue<string>());
    }

    private static async Task<WorkflowUpdateFailedException> Refused(WorkflowHandle handle, GraphAnswerInput answer)
    {
        using var limit = Limit();
        return await Assert.ThrowsAsync<WorkflowUpdateFailedException>(async () => await Answer(handle, answer, limit.Token));
    }

    // why: waits for the 'timeout' branch to be taken; the timer may start after the first skip, so skip until the node after the question runs
    private static async Task SkipUntilRunning(GraphRig rig, WorkflowRunId run, string node, TimeSpan step)
    {
        using var limit = Limit();
        while (!(await rig.NodeStatuses(run)).TryGetValue(node, out var status) || status != NodeRunStatus.Running)
        {
            await rig.Environment.DelayAsync(step);
            await Task.Delay(TimeSpan.FromMilliseconds(100), limit.Token);
        }
    }

    private static async Task WaitUntilAsked(GraphRig rig)
    {
        using var limit = Limit();
        while (rig.Questions.Asked.Count == 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), limit.Token);
        }
    }

    [Fact]
    public async Task A_cancel_ends_an_ask_owner_node_cancelled_and_skips_the_rest()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(AskSpec());
        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        await WaitUntilAsked(rig);

        await rig.Client.GetWorkflowHandle(started.RunId.Value).CancelAsync();
        var run = await rig.Final(started.RunId);
        var nodes = await rig.NodeStatuses(started.RunId);

        Assert.Equal(WorkflowRunStatus.Cancelled, run.Status);
        Assert.Equal(NodeRunStatus.Cancelled, nodes["ask"]);
        Assert.Equal(NodeRunStatus.Skipped, nodes["yes"]);
        Assert.Equal(NodeRunStatus.Skipped, nodes["no"]);
        Assert.Empty(rig.Blocks.Log);
    }

    // why: the question is posted by the block's activity, a moment before the workflow parks; an answer sent in that gap waits for the park instead of being refused, so there is no retry here
    [Fact]
    public async Task An_answer_sent_as_soon_as_the_question_is_posted_is_accepted_without_a_retry()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(AskSpec());
        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        await WaitUntilAsked(rig);
        using var limit = Limit();

        var by = await Answer(rig.Client.GetWorkflowHandle(started.RunId.Value), new GraphAnswerInput("ask", Text: null, Choice: "yes", "session:early"), limit.Token);
        var result = await Result(rig, started.RunId);

        Assert.Equal("session:early", by);
        Assert.Equal(WorkflowRunStatus.Succeeded, result.Status);
        Assert.Equal(("yes", "session:early"), await AskOutput(rig, started.RunId));
        Assert.Equal([$"{rig.Slug}@1:1/yes"], rig.Blocks.Log);
    }

    [Fact]
    public async Task A_second_answer_to_the_same_question_is_refused_and_the_first_stands()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(HoldSpec("answered"));
        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        var handle = rig.Client.GetWorkflowHandle(started.RunId.Value);
        await AnswerWhenWaiting(rig, started.RunId, new GraphAnswerInput("ask", Text: null, Choice: "yes", "session:first"));
        await WaitUntilRunning(rig, started.RunId, "hold");

        var second = await Refused(handle, new GraphAnswerInput("ask", Text: null, Choice: "no", "session:second"));
        var kept = await AskOutput(rig, started.RunId);
        await handle.CancelAsync();
        _ = await rig.Final(started.RunId);   // why: a cancel left open when the rig stops its worker is a workflow task nobody polls, and the shared time-skipping server stops skipping time for every later test in the class

        Assert.Contains("not waiting", second.InnerException!.Message, StringComparison.Ordinal);
        Assert.Equal(("yes", "session:first"), kept);
    }

    [Fact]
    public async Task An_answer_after_the_timeout_branch_was_taken_is_refused_and_the_default_stands()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(HoldSpec("timeout"));
        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        var handle = rig.Client.GetWorkflowHandle(started.RunId.Value);
        await SkipUntilRunning(rig, started.RunId, "hold", TimeSpan.FromMinutes(61));

        var late = await Refused(handle, new GraphAnswerInput("ask", Text: null, Choice: "yes", "owner"));
        var kept = await AskOutput(rig, started.RunId);
        var nodes = await rig.NodeStatuses(started.RunId);
        await handle.CancelAsync();
        _ = await rig.Final(started.RunId);   // why: a cancel left open when the rig stops its worker is a workflow task nobody polls, and the shared time-skipping server stops skipping time for every later test in the class

        Assert.Contains("not waiting", late.InnerException!.Message, StringComparison.Ordinal);
        Assert.Equal(("no", "timeout"), kept);
        Assert.Equal(NodeRunStatus.Skipped, nodes["other"]);
    }

    // why: whether the answer or the timer wins here depends on timing and may differ between runs; what must hold either way is that exactly one outcome stands and the node row says which:
    // an update the workflow accepted is the answer (answeredBy is who gave it), a refused one means the timeout (answeredBy 'timeout'), and only that branch ran
    [Fact]
    public async Task An_answer_at_the_moment_of_the_timeout_gives_one_outcome_and_the_row_says_which()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(AskSpec());
        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);

        var accepted = TryAnswer(rig, started.RunId, new GraphAnswerInput("ask", Text: null, Choice: "yes", "owner"));
        await SkipUntilEnded(rig, started.RunId, TimeSpan.FromMinutes(61));
        var answered = await accepted;
        var result = await Result(rig, started.RunId);
        var output = await AskOutput(rig, started.RunId);

        Assert.Equal(WorkflowRunStatus.Succeeded, result.Status);
        Assert.Equal(answered ? ("yes", "owner") : ("no", "timeout"), output);
        Assert.Equal([$"{rig.Slug}@1:1/{(answered ? "yes" : "no")}"], rig.Blocks.Log);
    }

    // why: true when the workflow accepted the answer; false when it refused because the question had ended, which is told from the ask node no longer running
    private static async Task<bool> TryAnswer(GraphRig rig, WorkflowRunId run, GraphAnswerInput answer)
    {
        using var limit = Limit();
        var handle = rig.Client.GetWorkflowHandle(run.Value);
        while (true)
        {
            try
            {
                _ = await Answer(handle, answer, limit.Token);
                return true;
            }
            catch (Exception e) when (e is WorkflowUpdateFailedException or RpcException)
            {
                if ((await rig.NodeStatuses(run)).TryGetValue("ask", out var status) && status != NodeRunStatus.Running)
                {
                    return false;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50), limit.Token);
            }
        }
    }
}
