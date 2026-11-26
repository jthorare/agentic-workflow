namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): Compare, two ticked runs side by side (#3683).</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>True on the Compare view of two runs.</summary>
    public bool IsCompare => _level == Level.Compare;

    /// <summary>The two runs compared.</summary>
    public CompareViewModel Compare { get; private set => Set(ref field, value); } = new();

    /// <summary>Compare on exactly two ticked runs: reads both and opens them side by side, the newer one as Run A. Any other count does nothing.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the comparison is shown.</returns>
    internal async Task OpenCompare(CancellationToken cancellationToken)
    {
        if (_url is not { } url || Runs.Ticked is not [var newer, var older])
        {
            return;
        }

        var reads = await Task.WhenAll(Api.WorkflowRun(url, newer.Id, cancellationToken), Api.WorkflowRun(url, older.Id, cancellationToken)).ConfigureAwait(true);
        foreach (var read in reads)
        {
            if (read.IsError)
            {
                Runs.Note(Labels.WfRunFailed(read.Error));
                return;
            }
        }

        Compare = new CompareViewModel(reads[0].Value, reads[1].Value, Runs.NodeKinds, _clock);
        SetLevel(Level.Compare);
    }
}
