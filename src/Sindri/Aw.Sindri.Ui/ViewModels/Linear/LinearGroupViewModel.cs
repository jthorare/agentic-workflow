#pragma warning disable CA1822, MA0041, MA0182, CS0067 // why: the red stubs read nothing yet

using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>A group of the list: Assigned to you, or Your teams.</summary>
internal sealed class LinearGroupViewModel : ObservableObject
{
    internal LinearGroupViewModel(string heading)
    {
        throw new NotSupportedException();
    }

    /// <summary>The heading.</summary>
    public string Heading => throw new NotSupportedException();

    /// <summary>How many issues match.</summary>
    public string Count => throw new NotSupportedException();

    /// <summary>The loaded rows.</summary>
    public ObservableCollection<LinearIssueRowViewModel> Rows => throw new NotSupportedException();

    /// <summary>The line shown while the group has no row; empty when it says nothing.</summary>
    public string Empty => throw new NotSupportedException();

    /// <summary>True when the group shows rows or a line.</summary>
    public bool IsVisible => throw new NotSupportedException();
}
