using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Runs;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The parking blocks (issue 2258, PR 9). A 'wait' node sleeps on a Temporal timer, an 'ask_owner' node waits for the 'Answer' update or its timeout; neither polls.
/// Both park after their block has answered, while the node is still 'running', so a cancel (which cancels the timer or the wait) ends the node 'cancelled'.
/// </summary>
internal sealed partial class GraphWorkflow
{
    private readonly Dictionary<string, GraphAnswerInput> _answers = new(StringComparer.Ordinal);
    private string? _asking;
    private IReadOnlyList<string> _choices = [];

    /// <summary>
    /// Takes the answer to the 'ask_owner' node now waiting. A node whose question is posted but whose wait has not started yet (the block activity is still returning) gets a short bounded wait first,
    /// so an agent that saw the question can answer it at once.
    /// </summary>
    /// <param name="input">The answer.</param>
    /// <returns>Who answered.</returns>
    [WorkflowUpdate("Answer")]
    public async Task<string> AnswerAsync(GraphAnswerInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!string.Equals(_asking, input.Node, StringComparison.Ordinal))
        {
            var reached = await Workflow.WaitConditionAsync(() => string.Equals(_asking, input.Node, StringComparison.Ordinal), _askingGrace).ConfigureAwait(true);
            if (!reached)
            {
                throw NotReached(input.Node);
            }
        }

        Refuse(input);   // why: the state may have moved while the update waited
        _answers[input.Node] = input;
        return input.By;
    }

    /// <summary>Refuses an answer when no question waits or can soon wait at that node, it was answered already, or (once the node is waiting) the answer is not one it takes.</summary>
    /// <param name="input">The answer.</param>
    [WorkflowUpdateValidator(nameof(AnswerAsync))]
    public void CheckAnswer(GraphAnswerInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var starting = _current is { } current && string.Equals(current.Type, "ask_owner", StringComparison.Ordinal) && string.Equals(current.Id.Value, input.Node, StringComparison.Ordinal);
        if (!starting && !string.Equals(_asking, input.Node, StringComparison.Ordinal))
        {
            throw NotWaiting(input.Node);
        }

        Refuse(input, requireAsking: false);
    }

    private static readonly TimeSpan _askingGrace = TimeSpan.FromSeconds(5);

    // why: the grace ran out before the node posted its question, so the answer is early, not late; the text says to retry
    private static ApplicationFailureException NotReached(string node) => new($"'{node}' is not waiting for an answer yet: the run has not reached that question. Retry in a few seconds", errorType: AnswerRefusal.NotWaiting, nonRetryable: true);

    private static ApplicationFailureException NotWaiting(string node) => new($"'{node}' is not waiting for an answer", errorType: AnswerRefusal.NotWaiting, nonRetryable: true);

    // why: the choices are known only once the node waits, so a node that has not started waiting yet is checked again by the handler
    private void Refuse(GraphAnswerInput input, bool requireAsking = true)
    {
        var waiting = string.Equals(_asking, input.Node, StringComparison.Ordinal);
        if (_answers.ContainsKey(input.Node) || (requireAsking && !waiting))
        {
            throw NotWaiting(input.Node);
        }

        if (waiting && AskOwnerBlock.CheckAnswer(_choices, input.Text, input.Choice) is { } problem)
        {
            throw new ApplicationFailureException(problem, errorType: AnswerRefusal.BadAnswer, nonRetryable: true);
        }
    }

    // why: the node's block ran as an activity and succeeded; what is left is the waiting, which only the workflow may do
    private async Task<GraphBlockOutcome> Park(GraphNode node, GraphBlockOutcome outcome)
    {
        if (outcome.Status != NodeRunStatus.Succeeded || outcome.Fenced)
        {
            return outcome;   // why: a test run's block activity already answered the wait or the question (GraphTestWalk), so nothing parks
        }

        return node.Type switch
        {
            "wait" => await Sleep(outcome).ConfigureAwait(true),
            "ask_owner" => await Ask(node, outcome).ConfigureAwait(true),
            _ => outcome,
        };
    }

    // why: the sleep is the block's own 'waitedSeconds' (wakeAt minus the block's clock), never wakeAt minus Workflow.UtcNow: the block and
    // the Temporal server keep separate clocks, so comparing them can end a wait at once (it did: the parking tests' fake clock sat months
    // behind the server and every wait node finished before anyone saw it running)
    internal static async Task<GraphBlockOutcome> Sleep(GraphBlockOutcome outcome)
    {
        var seconds = JsonNode.Parse(outcome.Output ?? "{}")?["waitedSeconds"] is JsonValue v && v.TryGetValue<double>(out var wait) ? wait : 0;
        if (seconds > 0)
        {
            await Workflow.DelayAsync(TimeSpan.FromSeconds(seconds)).ConfigureAwait(true);
        }

        return outcome;
    }

    private async Task<GraphBlockOutcome> Ask(GraphNode node, GraphBlockOutcome outcome)
    {
        var config = JsonNode.Parse(outcome.Input ?? "{}") as JsonObject ?? [];
        var id = node.Id.Value;
        _asking = id;
        _choices = AskOwnerBlock.ChoicesOf(config);
        try
        {
            if (AskOwnerBlock.TimeoutOf(config) is { } limit)
            {
                _ = await Workflow.WaitConditionAsync(() => _answers.ContainsKey(id), limit).ConfigureAwait(true);
            }
            else
            {
                await Workflow.WaitConditionAsync(() => _answers.ContainsKey(id)).ConfigureAwait(true);
            }
        }
        finally
        {
            _asking = null;
        }

        // why: an answer that arrived as the timer fired still wins; the update handler ran, so the map has it
        var resolved = _answers.TryGetValue(id, out var given)
            ? AskOwnerBlock.Resolve(config, given.Text ?? given.Choice, given.By)
            : AskOwnerBlock.Resolve(config, answer: null, answeredBy: "timeout");
        return outcome with { Output = resolved.Outputs.ToJsonString(), Taken = [resolved.Branch] };
    }
}
