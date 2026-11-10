namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>One fixed group of the since-you-looked card: a heading and its rows, newest first. The card shows the first five; Show all expands the group.</summary>
/// <param name="heading">"Assigned to you", "Moved" or "Other".</param>
/// <param name="rows">The issues' rows, newest change first.</param>
/// <param name="expanded">True when the group was open before this read: a re-read keeps what Show all opened.</param>
internal sealed class LinearSinceGroupViewModel(string heading, IReadOnlyList<LinearSinceRowViewModel> rows, bool expanded = false) : ObservableObject
{
    /// <summary>How many rows a group shows before Show all.</summary>
    internal const int Cap = 5;

    /// <summary>The group's heading.</summary>
    public string Heading { get; } = heading;

    /// <summary>How many issues the group holds, as the heading's count.</summary>
    public string Count { get; } = rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The rows, newest first.</summary>
    public IReadOnlyList<LinearSinceRowViewModel> Rows { get; } = rows;

    /// <summary>True once Show all expanded the group.</summary>
    public bool IsExpanded { get; private set; } = expanded;

    /// <summary>The rows the card shows: the first five, or all of them once expanded.</summary>
    public IReadOnlyList<LinearSinceRowViewModel> ShownRows => IsExpanded ? Rows : [.. Rows.Take(Cap)];

    /// <summary>True when the group holds more rows than the card shows at first.</summary>
    public bool HasMore => Rows.Count > Cap;

    /// <summary>"Show all", or "Show fewer" once expanded.</summary>
    public string ToggleText => IsExpanded ? Labels.LinearShowFewer : Labels.LinearShowAll;

    /// <summary>Expands the group, or folds it back to five rows.</summary>
    internal void Toggle()
    {
        IsExpanded = !IsExpanded;
        OnChanged(nameof(IsExpanded));
        OnChanged(nameof(ShownRows));
        OnChanged(nameof(ToggleText));
    }
}
