using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>One group of rows on the Projects page ("Off track 3", "In progress 6"), or the one group with no head when the rows are not grouped.</summary>
internal sealed class ProjectGroupViewModel : ObservableObject
{
    internal ProjectGroupViewModel(string heading)
    {
        Heading = heading;
    }

    /// <summary>The head's words; empty for the one group with no head.</summary>
    public string Heading { get; }

    /// <summary>True when the group draws its head.</summary>
    public bool HasHeading => Heading.Length > 0;

    /// <summary>The words on the right of the head ("derived from each project’s open PRs" on the first health group); empty for none.</summary>
    public string Hint { get; internal set => Set(ref field, value); } = string.Empty;

    /// <summary>How many rows.</summary>
    public int Count => Cards.Count;

    /// <summary>The head's accessible name: "Off track, 3, expanded".</summary>
    public string HeadingName => Labels.ProjectsGroupToggleName(Heading, Count, IsCollapsed);

    /// <summary>True while the group's rows are folded under its head; kept across reads (the page keeps the group by its key).</summary>
    public bool IsCollapsed
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(IsExpanded));
            OnChanged(nameof(Chevron));
            OnChanged(nameof(HeadingName));
        }
    }

    /// <summary>True while the rows show.</summary>
    public bool IsExpanded => !IsCollapsed;

    /// <summary>The head's chevron: ▾ open, ▸ folded.</summary>
    public string Chevron => IsCollapsed ? "▸" : "▾";

    /// <summary>Folds or unfolds the group's rows (a click or Enter on its head).</summary>
    internal void Toggle() => IsCollapsed = !IsCollapsed;

    /// <summary>The rows, in order.</summary>
    public ObservableCollection<ProjectCardViewModel> Cards { get; } = [];

    /// <summary>Shows these rows.</summary>
    /// <param name="cards">The rows, in order.</param>
    internal void Show(IReadOnlyList<ProjectCardViewModel> cards)
    {
        Patch.To(Cards, cards);
        OnChanged(nameof(Count));
        OnChanged(nameof(HeadingName));
    }
}
