using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>One issue of the list: the words a row says, from the issue it was last read as. A read of the same issue updates the row in place, so focus stays on it.</summary>
internal sealed class LinearIssueRowViewModel : ObservableObject
{
    private static readonly string[] _all =
    [
        nameof(Title), nameof(StateName), nameof(AssigneeName), nameof(PriorityText), nameof(PrText), nameof(HasPr), nameof(RunState), nameof(Name),
    ];

    private LinearIssueRow _row;

    internal LinearIssueRowViewModel(LinearIssueRow row)
    {
        _row = row;
    }

    /// <summary>The issue key.</summary>
    public string Key => _row.Key;

    /// <summary>The title.</summary>
    public string Title => _row.Title;

    /// <summary>The state's name.</summary>
    public string StateName => _row.State.Name;

    /// <summary>The assignee's name, or "Unassigned".</summary>
    public string AssigneeName => _row.Assignee?.Name ?? Labels.LinearUnassigned;

    /// <summary>The priority in words (a chip carries text, never colour alone).</summary>
    public string PriorityText => Labels.LinearPriority(_row.Priority);

    /// <summary>"PR #2071 waiting on you"; empty without a PR.</summary>
    public string PrText => _row.Pr is { } pr ? Labels.LinearPrChip(pr.Number, pr.Review) : string.Empty;

    /// <summary>True when a PR is linked.</summary>
    public bool HasPr => _row.Pr is not null;

    /// <summary>The linked item's state; empty when no item works the issue.</summary>
    public string RunState => _row.Link?.ItemState ?? string.Empty;

    /// <summary>The row's accessible name.</summary>
    public string Name => Labels.LinearRowName(_row);

    /// <summary>Takes a newer read of the same issue.</summary>
    /// <param name="row">The row.</param>
    internal void Update(LinearIssueRow row)
    {
        _row = row;
        foreach (var name in _all)
        {
            OnChanged(name);
        }
    }
}
