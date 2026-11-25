using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>
/// One fixed group of the since-you-looked card: a heading and its rows, newest first. The card shows the first five; Show all expands the group.
/// A re-read keeps the group and its row containers (<see cref="Update"/>), so a tabbed-to button or row keeps keyboard focus (UI spec §3.3).
/// </summary>
/// <param name="heading">"Assigned to you", "Moved" or "Other".</param>
internal sealed class LinearSinceGroupViewModel(string heading) : ObservableObject
{
    /// <summary>How many rows a group shows before Show all.</summary>
    internal const int Cap = 5;

    /// <summary>The group's heading.</summary>
    public string Heading { get; } = heading;

    /// <summary>How many issues the group holds, as the heading's count.</summary>
    public string Count { get; private set => Set(ref field, value); } = "0";

    /// <summary>The rows, newest first.</summary>
    public IReadOnlyList<LinearSinceRowViewModel> Rows { get; private set; } = [];

    /// <summary>True once Show all expanded the group.</summary>
    public bool IsExpanded { get; private set; }

    /// <summary>The rows the card shows: the first five, or all of them once expanded. Patched in place, so kept rows keep their containers.</summary>
    public ObservableCollection<LinearSinceRowViewModel> ShownRows { get; } = [];

    /// <summary>True when the group holds more rows than the card shows at first.</summary>
    public bool HasMore => Rows.Count > Cap;

    /// <summary>"Show all", or "Show fewer" once expanded.</summary>
    public string ToggleText => IsExpanded ? Labels.LinearShowFewer : Labels.LinearShowAll;

    /// <summary>Takes the rows of a new read, keeping the group's open state.</summary>
    /// <param name="rows">The group's rows, newest change first.</param>
    internal void Update(IReadOnlyList<LinearSinceRowViewModel> rows)
    {
        Rows = rows;
        Count = rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        OnChanged(nameof(Rows));
        OnChanged(nameof(HasMore));
        Fit();
    }

    /// <summary>Expands the group, or folds it back to five rows.</summary>
    internal void Toggle()
    {
        IsExpanded = !IsExpanded;
        OnChanged(nameof(IsExpanded));
        Fit();
    }

    private void Fit()
    {
        Patch.To(ShownRows, IsExpanded ? Rows : [.. Rows.Take(Cap)]);
        OnChanged(nameof(ToggleText));
    }
}
