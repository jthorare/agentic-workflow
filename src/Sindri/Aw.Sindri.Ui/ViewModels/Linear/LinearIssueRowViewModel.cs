#pragma warning disable CA1822, MA0041, MA0182, CS0067 // why: the red stubs read nothing yet

using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>One issue of the list.</summary>
internal sealed class LinearIssueRowViewModel : ObservableObject
{
    internal LinearIssueRowViewModel(LinearIssueRow row)
    {
        throw new NotSupportedException();
    }

    /// <summary>The issue key.</summary>
    public string Key => throw new NotSupportedException();

    /// <summary>The title.</summary>
    public string Title => throw new NotSupportedException();

    /// <summary>The state's name.</summary>
    public string StateName => throw new NotSupportedException();

    /// <summary>The assignee's name, or "Unassigned".</summary>
    public string AssigneeName => throw new NotSupportedException();

    /// <summary>The priority in words.</summary>
    public string PriorityText => throw new NotSupportedException();

    /// <summary>"PR #2071 waiting on you"; empty without a PR.</summary>
    public string PrText => throw new NotSupportedException();

    /// <summary>True when a PR is linked.</summary>
    public bool HasPr => throw new NotSupportedException();

    /// <summary>The linked item's state; empty when none.</summary>
    public string RunState => throw new NotSupportedException();

    /// <summary>The row's accessible name.</summary>
    public string Name => throw new NotSupportedException();

    /// <summary>Takes a newer read of the same issue.</summary>
    /// <param name="row">The row.</param>
    internal void Update(LinearIssueRow row) => throw new NotSupportedException();
}
