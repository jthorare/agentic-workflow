using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

internal sealed partial class WorkflowsViewModel
{
    // The filter the Done rows were read for; null while they are not current (never read, collapsed, or a refresh passed without reading them).
    private string? _doneFor;

    private string DoneKey => $"{RecipeFilter}\n{SubjectFilter}";

    private async Task ReadDone(CancellationToken cancellationToken)
    {
        if (Url is not { } url)
        {
            return;
        }

        var read = ++_doneReads;
        var key = DoneKey;
        var all = await api.Instances(url, new InstanceQuery(Root: true, Open: false, Recipe: RecipeFilter, Subject: SubjectFilter), cancellationToken).ConfigureAwait(true);
        if (read != _doneReads || !(IsDoneExpanded || ViewMode == WorkflowViewMode.Board))
        {
            return;   // newer, or the line was collapsed while this was on its way
        }

        if (all.IsSuccess)
        {
            var before = WalkAddresses();
            var finished = all.Value.Instances.Where(r => r.States.Any(s => s.Terminal && string.Equals(s.State, r.State, StringComparison.Ordinal))).Select(r => new InstanceRowViewModel(r, clipboard, clock)).ToList();
            finished.ForEach(r => r.SetStale(IsStale));
            DoneRows.Clear();
            finished.ForEach(DoneRows.Add);
            _doneFor = key;
            RebuildBoard();
            Reselect(before);
        }
    }

    private void MarkUnreadable()
    {
        IsUnreadable = true;
        OnChanged(nameof(UnreadableText));
        NotifyBoardStates();
        // The last good data stays, marked stale (screens §4.1): the next readable answer rebuilds nothing the human was on.
        foreach (var row in _rows.Values.Concat(DoneRows))
        {
            row.SetStale(stale: true);
        }
    }
}
