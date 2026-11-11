using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>An 'ask_owner' node in a 'for_each' body asks once per item, in the batch's child workflow, and an answer reaches that child by node and item (#2543).</summary>
public sealed partial class GraphWorkflowParkingTests
{
    private static GraphSpec AskInLoopSpec() => GraphRig.Spec("unlimited",
        [GraphRig.Node("start", "manual"), GraphRig.Node("each", "for_each", config: new JsonObject { ["items"] = "{{trigger.items}}" }),
            GraphRig.Node("ask", "ask_owner", config: new JsonObject { ["question"] = "Ship {{item}}?", ["choices"] = new JsonArray("yes", "no") }), GraphRig.Node("gather", "collect", config: new JsonObject { ["value"] = "{{ask.answer}}" })],
        [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "ask"), GraphRig.Edge("ask", "gather")]);

    private static async Task<Core.Graph.Blocks.QuestionAsk> NthQuestion(GraphRig rig, int count)
    {
        using var limit = Limit();
        while (rig.Questions.Posted.Count < count)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), limit.Token);
        }

        return rig.Questions.Posted[count - 1];
    }

    [Fact]
    public async Task Two_items_of_one_batch_each_ask_and_an_answer_by_node_and_item_reaches_the_batch_child()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(AskInLoopSpec());
        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, TwoItems);
        var child = $"{started.RunId.Value}/each/1";

        var first = await NthQuestion(rig, 1);
        using var limit = Limit();
        _ = await Answer(rig.Client.GetWorkflowHandle(first.BatchWorkflow!), new GraphAnswerInput("ask", Text: null, "yes", "human:test") { Item = 1 }, limit.Token);
        var second = await NthQuestion(rig, 2);
        _ = await Answer(rig.Client.GetWorkflowHandle(second.BatchWorkflow!), new GraphAnswerInput("ask", Text: null, "no", "human:test") { Item = 2 }, limit.Token);
        var run = await rig.Final(started.RunId);
        var asks = await rig.ItemRuns(started.RunId, "ask");

        Assert.Equal((1, child), (first.Item, first.BatchWorkflow));
        Assert.Equal((2, child), (second.Item, second.BatchWorkflow));
        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal([NodeRunStatus.Succeeded, NodeRunStatus.Succeeded], new[] { asks[1].Status, asks[2].Status });
        Assert.Equal("yes", JsonNode.Parse(asks[1].Output!)!["answer"]!.GetValue<string>());
        Assert.Equal("no", JsonNode.Parse(asks[2].Output!)!["answer"]!.GetValue<string>());
    }

    [Fact]
    public async Task An_answer_for_the_wrong_item_is_refused_and_the_question_stays_open()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(AskInLoopSpec());
        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, TwoItems);
        var first = await NthQuestion(rig, 1);

        var refused = await Refused(rig.Client.GetWorkflowHandle(first.BatchWorkflow!), new GraphAnswerInput("ask", Text: null, "yes", "human:test") { Item = 2 });
        using var limit = Limit();
        _ = await Answer(rig.Client.GetWorkflowHandle(first.BatchWorkflow!), new GraphAnswerInput("ask", Text: null, "yes", "human:test") { Item = 1 }, limit.Token);
        _ = await NthQuestion(rig, 2);
        await rig.Client.GetWorkflowHandle(started.RunId.Value).CancelAsync();
        _ = await rig.Final(started.RunId);

        Assert.Contains("not waiting", refused.InnerException!.Message, StringComparison.Ordinal);
    }
}
