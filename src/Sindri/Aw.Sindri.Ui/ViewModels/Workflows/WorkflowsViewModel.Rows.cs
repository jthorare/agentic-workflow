using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

internal sealed partial class WorkflowsViewModel
{
    private RecipeGroupViewModel Group(string recipe, List<InstanceRowViewModel> rows)
    {
        if (!_groups.TryGetValue(recipe, out var group))
        {
            group = new RecipeGroupViewModel(recipe);
            group.RecipeRequested += (_, e) => RecipeRequested?.Invoke(this, e);
            _groups[recipe] = group;
        }

        group.Show(rows);
        return group;
    }

    // A re-read serves the same row again: it stays (so does the control with focus) unless something it draws changed.
    private InstanceRowViewModel Row(InstanceSummary summary)
    {
        var row = new InstanceRowViewModel(summary, clipboard, clock);
        var old = Groups.SelectMany(g => g.Rows).FirstOrDefault(r => string.Equals(r.Address, row.Address, StringComparison.Ordinal));
        var kept = old is not null && string.Equals(old.Signature, row.Signature, StringComparison.Ordinal) ? old : row;
        kept.SetStale(IsStale);
        return kept;
    }
}
