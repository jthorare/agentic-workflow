namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>Take over and Hand back on an agent node (<see cref="RunDetailViewModel"/>, continued).</summary>
internal sealed partial class RunDetailViewModel
{
    /// <summary>
    /// Take over or Hand back on one agent node: sent once (a press while one is on the way does nothing), the note says what Sindri answered, and a success reads the run again.
    /// A refusal shows the server's text and reads nothing.
    /// </summary>
    /// <param name="node">The node run the button belongs to.</param>
    /// <param name="takeOver">True for Take over, false for Hand back.</param>
    /// <param name="cancellationToken">Cancels the call and the read.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    private async Task ControlNode(NodeRunViewModel node, bool takeOver, CancellationToken cancellationToken)
    {
        if (_url is not { } url || node.IsBusy || !(takeOver ? node.CanTakeOver : node.CanHandBack))
        {
            return;
        }

        var id = Id;
        node.IsBusy = true;
        Note(string.Empty);
        try
        {
            var (ok, note, agent) = await Send(url, id, node, takeOver, cancellationToken).ConfigureAwait(true);
            if (!string.Equals(Id, id, StringComparison.Ordinal))
            {
                return;   // another run is open now
            }

            Note(note);
            if (ok)
            {
                await Read(url, id, cancellationToken).ConfigureAwait(true);
                KeepAnswered(node, agent);
            }
        }
        finally
        {
            node.IsBusy = false;
        }
    }

    // The read can lag the call (the engine writes its marker late), so what Sindri answered wins on the rebuilt row of the same node and item.
    // A row the read shows with no agent has ended since the call: it keeps no button.
    private void KeepAnswered(NodeRunViewModel node, string? agent)
    {
        var row = Groups.FirstOrDefault(g => string.Equals(g.Node, node.Node, StringComparison.Ordinal))?.Runs.FirstOrDefault(r => r.ItemIndex == node.ItemIndex);
        if (row is { Agent: not null } && agent is not null)
        {
            row.Agent = agent;
        }
    }

    private async Task<(bool Ok, string Note, string? Agent)> Send(Uri url, string id, NodeRunViewModel node, bool takeOver, CancellationToken cancellationToken)
    {
        var sent = takeOver
            ? await api.TakeOverAgentNode(url, id, node.Node, node.ItemIndex, cancellationToken).ConfigureAwait(true)
            : await api.HandBackAgentNode(url, id, node.Node, node.ItemIndex, cancellationToken).ConfigureAwait(true);
        if (sent.IsSuccess)
        {
            return (true, takeOver ? Labels.WfTakeOverDone : Labels.WfHandBackDone, sent.Value.Agent);
        }

        return (false, takeOver ? Labels.WfTakeOverRefused(sent.Error) : Labels.WfHandBackRefused(sent.Error), null);
    }
}
