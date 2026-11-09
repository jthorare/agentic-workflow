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

    /// <summary>Takes the answer to the 'ask_owner' node now waiting; the validator has already checked it.</summary>
    /// <param name="input">The answer.</param>
    /// <returns>Who answered.</returns>
    [WorkflowUpdate("Answer")]
    public Task<string> AnswerAsync(GraphAnswerInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _answers[input.Node] = input;
        return Task.FromResult(input.By);
    }

    /// <summary>Refuses an answer when no question waits at that node, it was answered already, or the answer is not one the node takes.</summary>
    /// <param name="input">The answer.</param>
    [WorkflowUpdateValidator(nameof(AnswerAsync))]
    public void CheckAnswer(GraphAnswerInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!string.Equals(_asking, input.Node, StringComparison.Ordinal) || _answers.ContainsKey(input.Node))
        {
            throw new ApplicationFailureException($"'{input.Node}' is not waiting for an answer", errorType: AnswerRefusal.NotWaiting, nonRetryable: true);
        }

        if (AskOwnerBlock.CheckAnswer(_choices, input.Text, input.Choice) is { } problem)
        {
            throw new ApplicationFailureException(problem, errorType: AnswerRefusal.BadAnswer, nonRetryable: true);
        }
    }

    // why: the node's block ran as an activity and succeeded; what is left is the waiting, which only the workflow may do
    private async Task<GraphBlockOutcome> Park(GraphNode node, GraphBlockOutcome outcome)
    {
        if (outcome.Status != NodeRunStatus.Succeeded)
        {
            return outcome;
        }

        return node.Type switch
        {
            "wait" => await Sleep(outcome).ConfigureAwait(true),
            "ask_owner" => await Ask(node, outcome).ConfigureAwait(true),
            _ => outcome,
        };
    }

    private static async Task<GraphBlockOutcome> Sleep(GraphBlockOutcome outcome)
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
            : AskOwnerBlock.Resolve(config, answer: null, answeredBy: null);
        return outcome with { Output = resolved.Outputs.ToJsonString(), Taken = [resolved.Branch] };
    }
}
