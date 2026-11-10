using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>One issue of the list: the words a row says, from the issue it was last read as. A read of the same issue updates the row in place, so focus stays on it.</summary>
internal sealed class LinearIssueRowViewModel : ObservableObject
{
    private static readonly string[] _all =
    [
        nameof(Title), nameof(StateName), nameof(AssigneeName), nameof(PriorityText), nameof(PrText), nameof(HasPr), nameof(RunState), nameof(Name),
    ];

    internal LinearIssueRowViewModel(LinearIssueRow row)
    {
        Issue = row;
    }

    /// <summary>The issue as last read: the since-you-looked card takes its priority, project and assignee from it.</summary>
    internal LinearIssueRow Issue { get; private set; }

    /// <summary>The issue key.</summary>
    public string Key => Issue.Key;

    /// <summary>The title.</summary>
    public string Title => Issue.Title;

    /// <summary>The state's name.</summary>
    public string StateName => Issue.State.Name;

    /// <summary>The assignee's name, or "Unassigned".</summary>
    public string AssigneeName => Issue.Assignee?.Name ?? Labels.LinearUnassigned;

    /// <summary>The priority in words (a chip carries text, never colour alone).</summary>
    public string PriorityText => Labels.LinearPriority(Issue.Priority);

    /// <summary>"PR #2071 waiting on you"; empty without a PR.</summary>
    public string PrText => Issue.Pr is { } pr ? Labels.LinearPrChip(pr.Number, pr.Review) : string.Empty;

    /// <summary>True when a PR is linked.</summary>
    public bool HasPr => Issue.Pr is not null;

    /// <summary>The linked item's state; empty when no item works the issue.</summary>
    public string RunState => Issue.Link?.ItemState ?? string.Empty;

    /// <summary>True while J / K have the row selected.</summary>
    public bool IsSelected { get; internal set => Set(ref field, value); }

    /// <summary>The row's accessible name.</summary>
    public string Name => Labels.LinearRowName(Issue);

    /// <summary>Takes a newer read of the same issue.</summary>
    /// <param name="row">The row.</param>
    internal void Update(LinearIssueRow row)
    {
        Issue = row;
        foreach (var name in _all)
        {
            OnChanged(name);
        }
    }
}
