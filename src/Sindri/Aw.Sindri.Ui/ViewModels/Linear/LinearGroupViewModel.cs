using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>A group of the list: Assigned to you, or Your teams.</summary>
internal sealed class LinearGroupViewModel(string heading) : ObservableObject
{
    /// <summary>The heading.</summary>
    public string Heading { get; } = heading;

    /// <summary>How many issues match, not only the loaded ones.</summary>
    public string Count { get; private set => Set(ref field, value); } = Labels.LinearGroupCount(0);

    /// <summary>The loaded rows.</summary>
    public ObservableCollection<LinearIssueRowViewModel> Rows { get; } = [];

    /// <summary>The line shown while the group has no row; empty when it says nothing.</summary>
    public string Empty { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the group shows rows or a line.</summary>
    public bool IsVisible => Rows.Count > 0 || Empty.Length > 0;

    /// <summary>Shows what a read found.</summary>
    /// <param name="rows">The rows, kept in place when already shown.</param>
    /// <param name="total">How many issues match; null when unknown.</param>
    /// <param name="empty">The line to show while there is no row.</param>
    internal void Show(IReadOnlyList<LinearIssueRowViewModel> rows, int? total, string empty)
    {
        Patch.To(Rows, rows);
        (Count, Empty) = (Labels.LinearGroupCount(total, rows.Count), rows.Count == 0 ? empty : string.Empty);
        OnChanged(nameof(IsVisible));
    }

    /// <summary>Adds the next page after the rows shown.</summary>
    /// <param name="more">The rows read.</param>
    /// <param name="total">How many issues match; null when unknown.</param>
    internal void Append(IReadOnlyList<LinearIssueRowViewModel> more, int? total)
    {
        Patch.To(Rows, [.. Rows, .. more.Where(r => !Rows.Contains(r))]);
        Count = Labels.LinearGroupCount(total, Rows.Count);
        OnChanged(nameof(IsVisible));
    }

    /// <summary>Shows the group before its first answer: nothing, and no line.</summary>
    internal void Clear() => Show([], total: 0, empty: string.Empty);
}
