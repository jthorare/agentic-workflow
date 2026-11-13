using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Loops;
using Aw.Sindri.Core.Graph.Runs;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// A batch of a 'for_each' (PR 9): for each of its items, in order, walks the loop's nodes (the body, then the 'collect') the way the run's walk does, one activity per
/// block execution, writing a node row per item (the item's position counted from 1 is the row's item index). The batch has its own history, and its size is fixed by the batch size.
/// The first node that fails and says 'stop' ends the batch; the nodes left for that item are 'skipped'.
/// </summary>
[Workflow("GraphIteration")]
internal sealed partial class GraphIterationWorkflow
{
    private static readonly ActivityOptions _write = GraphActivityOptions.Write;
    private static readonly ActivityOptions _detached = GraphActivityOptions.Detached;
    private static readonly ActivityOptions _block = GraphActivityOptions.Block;

    private WorkflowRunId _run;
    private GraphNode? _current;
    private int _item;
    private int _agentLaunchAttempts = GraphAgentLoop.DefaultAgentLaunchAttempts;
    private LoopShape? _shape;
    private int _last;
    private List<GraphNode> _left = [];
    private readonly Dictionary<(string Node, int Item), GraphAnswerInput> _answers = [];
    private (string Node, int Item)? _asking;
    private IReadOnlyList<string> _choices = [];

    /// <summary>Runs the batch.</summary>
    /// <param name="input">The batch.</param>
    /// <returns>How it ended: the first failure, or none.</returns>
    [WorkflowRun]
    public async Task<GraphIterationResult> RunAsync(GraphIterationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _run = input.Run;
        _agentLaunchAttempts = input.AgentLaunchAttempts > 0 ? input.AgentLaunchAttempts : GraphAgentLoop.DefaultAgentLaunchAttempts;
        _agents = new GraphAgentLoop(_run, _agentLaunchAttempts, Announce);
        try
        {
            var spec = await Workflow.ExecuteActivityAsync((GraphRunActivities a) => a.LoadAsync(_run), _write).ConfigureAwait(true);
            var found = LoopBody.Find(spec, input.ForEach);
            if (found.IsError)
            {
                throw new ApplicationFailureException(found.Error.Message, nonRetryable: true);
            }

            _shape = found.Value;
            _last = input.First + input.Count - 1;
            for (var item = input.First; item < input.First + input.Count; item++)
            {
                if (await Walk(spec, found.Value, item).ConfigureAwait(true) is { } failure)
                {
                    return new GraphIterationResult(failure);
                }
            }

            return new GraphIterationResult(Failure: null);
        }
        catch (Exception e) when (TemporalException.IsCanceledException(e))
        {
            await Cancelled().ConfigureAwait(true);
            throw;
        }
        catch (Exception e) when (e is not TemporalException && GraphFaults.Ends(e))
        {
            // why: an exception in workflow code is a workflow-task failure that Temporal retries without end; failing the batch lets the parent end the run (issue 2999). A write out of retries already fails the batch.
            throw new ApplicationFailureException(GraphFaults.Reason(e), e);
        }
    }

    private async Task Cancelled()
    {
        // why: the workflow's token is already cancelled, so every write here is detached, or it would never run and the rows would stay 'running'
        if (_current is { } running)
        {
            await Workflow.ExecuteActivityAsync((GraphRunActivities a) => a.RecordNodeAsync(Row(running, _item, NodeRunStatus.Cancelled)), _detached).ConfigureAwait(true);
        }

        await SkipUnfinished().ConfigureAwait(true);
    }

    private async Task<string?> Walk(GraphSpec spec, LoopShape shape, int item)
    {
        var decided = new HashSet<NodeId> { shape.ForEach.Id };
        var taken = new Dictionary<NodeId, IReadOnlyList<string>> { [shape.ForEach.Id] = ["out"] };
        var pending = shape.PerItem.ToList();
        _left = pending;
        _item = item;
        string? failure = null;
        while (failure is null && pending.Find(n => GraphWalkRules.Ready(spec, n, decided)) is { } node)
        {
            _ = pending.Remove(node);
            if (!GraphWalkRules.Live(spec, node, taken))
            {
                await Workflow.ExecuteActivityAsync((GraphRunActivities a) => a.RecordNodeAsync(Row(node, item, NodeRunStatus.Skipped)), _write).ConfigureAwait(true);
                _ = decided.Add(node.Id);
                continue;
            }

            var outcome = await Execute(node, item).ConfigureAwait(true);
            _ = decided.Add(node.Id);
            failure = ItemFailure(spec, node, item, outcome);
            taken[node.Id] = outcome.Status == NodeRunStatus.Succeeded ? outcome.Taken : ["out"];
        }

        foreach (var left in pending)
        {
            await Workflow.ExecuteActivityAsync((GraphRunActivities a) => a.RecordNodeAsync(Row(left, item, NodeRunStatus.Skipped)), _detached).ConfigureAwait(true);
        }

        return failure;
    }

    // why: on a cancel the rest of this item's nodes and every node of the batch's later items never run, so they are 'skipped'
    private async Task SkipUnfinished()
    {
        foreach (var node in _left.ToList())
        {
            await Workflow.ExecuteActivityAsync((GraphRunActivities a) => a.RecordNodeAsync(Row(node, _item, NodeRunStatus.Skipped)), _detached).ConfigureAwait(true);
            _ = _left.Remove(node);
        }

        if (_shape is { } shape)
        {
            for (var later = _item + 1; later <= _last; later++)
            {
                foreach (var node in shape.PerItem)
                {
                    await Workflow.ExecuteActivityAsync((GraphRunActivities a) => a.RecordNodeAsync(Row(node, later, NodeRunStatus.Skipped)), _detached).ConfigureAwait(true);
                }
            }
        }
    }

    private async Task<GraphBlockOutcome> Execute(GraphNode node, int item)
    {
        _current = node;
        _item = item;
        await Record(Row(node, item, NodeRunStatus.Running)).ConfigureAwait(true);
        var outcome = await RunBlock(node, item).ConfigureAwait(true);
        outcome = await Park(node, outcome).ConfigureAwait(true);

        // why: as in the run's walk, once the block has answered the node's result is decided, so the write is not cancelled with the workflow
        _current = null;
        await Workflow.ExecuteActivityAsync((GraphRunActivities a) => a.RecordNodeAsync(new NodeRun(_run, node.Id, item, outcome.Status, outcome.Input, outcome.Output, outcome.Truncated, outcome.Error, Usage: null, Raw: outcome.Raw)), _detached).ConfigureAwait(true);
        return outcome;
    }

    private async Task<GraphBlockOutcome> RunBlock(GraphNode node, int item)
    {
        try
        {
            // why: a session outlives a plain activity and a person may take it over, so a run_agent runs the agent loop in the workflow, one agent per item (#2470)
            if (string.Equals(node.Type, LoopBody.RunAgentType, StringComparison.Ordinal))
            {
                return await Agents.Run(node, item).ConfigureAwait(true);
            }

            return await Workflow.ExecuteActivityAsync((GraphBlockActivities a) => a.RunBlockAsync(_run, node.Id, item), _block).ConfigureAwait(true);
        }
        catch (ActivityFailureException e) when (!TemporalException.IsCanceledException(e))
        {
            return new GraphBlockOutcome(NodeRunStatus.Failed, Input: null, Output: null, [], e.InnerException?.Message ?? e.Message);
        }
    }

    // why: a 'wait' or an 'ask_owner' parks after its block answered, while the node is still 'running', so a cancel ends it 'cancelled'; the patches keep the commands of a body without one as before (#2490, #2543)
    private async Task<GraphBlockOutcome> Park(GraphNode node, GraphBlockOutcome outcome)
    {
        if (outcome.Status != NodeRunStatus.Succeeded)
        {
            return outcome;
        }

        if (string.Equals(node.Type, "wait", StringComparison.Ordinal) && Workflow.Patched("graph-iteration-wait"))
        {
            return await GraphWorkflow.Sleep(outcome).ConfigureAwait(true);   // why: the clock comment lives once, on Sleep
        }

        return string.Equals(node.Type, "ask_owner", StringComparison.Ordinal) && Workflow.Patched("graph-iteration-ask")
            ? await Ask(node, outcome).ConfigureAwait(true)
            : outcome;
    }

    /// <summary>
    /// Takes the answer to the 'ask_owner' node of the item now waiting; the question recorded this workflow's id, so an answer comes here by node and item. An item whose question is posted but whose wait
    /// has not started yet (the block activity is still returning) gets a short bounded wait first, like the run's own 'Answer'.
    /// </summary>
    /// <param name="input">The answer.</param>
    /// <returns>Who answered.</returns>
    [WorkflowUpdate("Answer")]
    public async Task<string> AnswerAsync(GraphAnswerInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (_asking != (input.Node, input.Item))
        {
            var reached = await Workflow.WaitConditionAsync(() => _asking == (input.Node, input.Item), _askingGrace).ConfigureAwait(true);
            if (!reached)
            {
                throw NotReached(input);
            }
        }

        Refuse(input);   // why: the state may have moved while the update waited
        _answers[(input.Node, input.Item)] = input;
        return input.By;
    }

    /// <summary>Refuses an answer when no question waits or can soon wait at that node and item, it was answered already, or (once the node is waiting) the answer is not one it takes.</summary>
    /// <param name="input">The answer.</param>
    [WorkflowUpdateValidator(nameof(AnswerAsync))]
    public void CheckAnswer(GraphAnswerInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var starting = _current is { } current && string.Equals(current.Type, "ask_owner", StringComparison.Ordinal) && string.Equals(current.Id.Value, input.Node, StringComparison.Ordinal) && _item == input.Item;
        if (!starting && _asking != (input.Node, input.Item))
        {
            throw NotWaiting(input);
        }

        Refuse(input, requireAsking: false);
    }

    private static readonly TimeSpan _askingGrace = TimeSpan.FromSeconds(5);

    // why: the grace ran out before the item posted its question, so the answer is early, not late; the text says to retry
    private static ApplicationFailureException NotReached(GraphAnswerInput input) =>
        new($"'{input.Node}' is not waiting for an answer for item {input.Item.ToString(System.Globalization.CultureInfo.InvariantCulture)} yet: the run has not reached that question. Retry in a few seconds", errorType: AnswerRefusal.NotWaiting, nonRetryable: true);

    private static ApplicationFailureException NotWaiting(GraphAnswerInput input) =>
        new($"'{input.Node}' is not waiting for an answer for item {input.Item.ToString(System.Globalization.CultureInfo.InvariantCulture)}", errorType: AnswerRefusal.NotWaiting, nonRetryable: true);

    private void Refuse(GraphAnswerInput input, bool requireAsking = true)
    {
        var waiting = _asking == (input.Node, input.Item);
        if (_answers.ContainsKey((input.Node, input.Item)) || (requireAsking && !waiting))
        {
            throw NotWaiting(input);
        }

        if (waiting && AskOwnerBlock.CheckAnswer(_choices, input.Text, input.Choice) is { } problem)
        {
            throw new ApplicationFailureException(problem, errorType: AnswerRefusal.BadAnswer, nonRetryable: true);
        }
    }

    private async Task<GraphBlockOutcome> Ask(GraphNode node, GraphBlockOutcome outcome)
    {
        var config = JsonNode.Parse(outcome.Input ?? "{}") as JsonObject ?? [];
        var key = (node.Id.Value, _item);
        _asking = key;
        _choices = AskOwnerBlock.ChoicesOf(config);
        try
        {
            if (AskOwnerBlock.TimeoutOf(config) is { } limit)
            {
                _ = await Workflow.WaitConditionAsync(() => _answers.ContainsKey(key), limit).ConfigureAwait(true);
            }
            else
            {
                await Workflow.WaitConditionAsync(() => _answers.ContainsKey(key)).ConfigureAwait(true);
            }
        }
        finally
        {
            _asking = null;
        }

        // why: an answer that arrived as the timer fired still wins; the update handler ran, so the map has it
        var resolved = _answers.TryGetValue(key, out var given)
            ? AskOwnerBlock.Resolve(config, given.Text ?? given.Choice, given.By)
            : AskOwnerBlock.Resolve(config, answer: null, answeredBy: "timeout");
        return outcome with { Output = resolved.Outputs.ToJsonString(), Taken = [resolved.Branch] };
    }

    private NodeRun Row(GraphNode node, int item, NodeRunStatus status) => new(_run, node.Id, item, status, Input: null, Output: null, Truncated: false, Error: null, Usage: null);

    private static Task Record(NodeRun row) => Workflow.ExecuteActivityAsync((GraphRunActivities a) => a.RecordNodeAsync(row), _write);
}
