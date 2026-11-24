using Aw.Sindri.Ui.Api.Projects;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>
/// The Projects page's rows (<see cref="ProjectsPageViewModel"/>, continued; issue 3717): the view shown (Needs me, My work, Projects, Triage), the sort,
/// the grouping, the tabs' counts, the column heads and the empty state of a view. The last list read is kept, so a new view, sort or grouping redraws
/// at once without a read.
/// </summary>
internal sealed partial class ProjectsPageViewModel
{
    private ProjectList? _list;
    private bool _viewPicked;

    /// <summary>The rows were laid out again (a read, a new view, sort or grouping): the shell hands the open sheet its project's row for the notes.</summary>
    internal event EventHandler? Laid;

    /// <summary>The views' tabs, keys 1 to 4.</summary>
    public IReadOnlyList<ProjectViewTab> Tabs { get; } =
    [
        new(ProjectView.NeedsMe, Labels.ProjectsViewNeedsMe),
        new(ProjectView.MyWork, Labels.ProjectsViewMyWork),
        new(ProjectView.All, Labels.ProjectsViewAll) { IsOn = true },
        new(ProjectView.Triage, Labels.ProjectsViewTriage),
    ];

    /// <summary>The Sort menu's choices.</summary>
    public IReadOnlyList<ProjectMenuChoice> SortChoices { get; } =
    [
        new(ProjectMenu.Sort, (int)ProjectSort.Activity, Labels.ProjectsSortActivity, Labels.ProjectsSortActivityHint) { IsOn = true },
        new(ProjectMenu.Sort, (int)ProjectSort.Target, Labels.ProjectsSortTarget, Labels.ProjectsSortTargetHint),
        new(ProjectMenu.Sort, (int)ProjectSort.Name, Labels.ProjectsSortName, Labels.ProjectsSortNameHint),
    ];

    /// <summary>The Group menu's choices.</summary>
    public IReadOnlyList<ProjectMenuChoice> GroupChoices { get; } =
    [
        new(ProjectMenu.Group, (int)ProjectGrouping.State, Labels.ProjectsGroupState, Labels.ProjectsGroupStateHint),
        new(ProjectMenu.Group, (int)ProjectGrouping.Health, Labels.ProjectsGroupHealth, Labels.ProjectsGroupHealthHint) { IsOn = true },
        new(ProjectMenu.Group, (int)ProjectGrouping.Repo, Labels.ProjectsGroupRepo, Labels.ProjectsGroupRepoHint),
        new(ProjectMenu.Group, (int)ProjectGrouping.None, Labels.ProjectsGroupNone, string.Empty),
    ];

    /// <summary>The view shown. Until the owner picks one: Needs me when the served truth carries the blocked-on-you flag (issue 3797), else Projects (an older server).</summary>
    public ProjectView View { get; private set => Set(ref field, value); } = ProjectView.All;

    /// <summary>The rows' order inside a group.</summary>
    public ProjectSort Sort { get; private set => Set(ref field, value); } = ProjectSort.Activity;

    /// <summary>How the rows are grouped.</summary>
    public ProjectGrouping Grouping { get; private set => Set(ref field, value); } = ProjectGrouping.Health;

    /// <summary>The Sort button's words: the sort in force.</summary>
    public string SortText => SortChoices.First(c => c.IsOn).Text;

    /// <summary>The Group button's words: the grouping in force.</summary>
    public string GroupText => GroupChoices.First(c => c.IsOn).Text;

    /// <summary>The Sort button's accessible name: "Sort: Last activity".</summary>
    public string SortName => Labels.ProjectsMenuButtonName(Labels.ProjectsSortLabel, SortText);

    /// <summary>The Group button's accessible name: "Group: Health".</summary>
    public string GroupName => Labels.ProjectsMenuButtonName(Labels.ProjectsGroupLabel, GroupText);

    /// <summary>True when Sindri couldn't read its own PR, check and agent facts: rows group by state instead of health or repo, and the one line below says so.</summary>
    public bool FactsMissing { get; private set => Set(ref field, value); }

    /// <summary>True when the page draws the line that the facts are missing.</summary>
    public bool ShowsFactsMissing { get; private set => Set(ref field, value); }

    /// <summary>The line drawn instead of health groups when the facts are missing, with "· grouped by state until then" while the owner's pick is Health or Repo.</summary>
    public string FactsMissingText => Grouping is ProjectGrouping.Health or ProjectGrouping.Repo
        ? Labels.ProjectSindriFactsMissing + Labels.ProjectsGroupedByStateUntilThen
        : Labels.ProjectSindriFactsMissing;

    /// <summary>True when the column heads and rows are drawn: the view has rows.</summary>
    public bool HasRows { get; private set => Set(ref field, value); }

    /// <summary>True when the view shown has no row and the page is not the served empty block: its own empty state.</summary>
    public bool IsViewEmpty { get; private set => Set(ref field, value); }

    /// <summary>The view's empty state: its title.</summary>
    public string ViewEmptyTitle { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The view's empty state: what the view holds.</summary>
    public string ViewEmptyBody { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The view's empty state: its button's words ("Open Projects", "Back to Needs me").</summary>
    public string ViewEmptyAction { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The key of the view the empty state's button shows ("3", "1").</summary>
    public string ViewEmptyKey { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the Finished and Hidden folds belong to the view: on Projects only.</summary>
    public bool ShowsFolds => View == ProjectView.All;

    /// <summary>The footer's key hints.</summary>
    public IReadOnlyList<ProjectKeyHint> KeyHints { get; private set => Set(ref field, value); } = [.. Labels.ProjectsKeyHints.Select(h => new ProjectKeyHint(h.Key, h.Word))];

#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>The Sort button's label.</summary>
    public string SortLabel => Labels.ProjectsSortLabel;

    /// <summary>The Group button's label.</summary>
    public string GroupLabel => Labels.ProjectsGroupLabel;

    /// <summary>The Sort menu's heading.</summary>
    public string SortBy => Labels.ProjectsSortBy;

    /// <summary>The Group menu's heading.</summary>
    public string GroupBy => Labels.ProjectsGroupBy;

    /// <summary>The Project column's head.</summary>
    public string ColumnProject => Labels.ProjectsColumnProject;

    /// <summary>The PRs column's head.</summary>
    public string ColumnPrs => Labels.ProjectsColumnPrs;

    /// <summary>The Items column's head.</summary>
    public string ColumnItems => Labels.ProjectsColumnItems;

    /// <summary>The Agents column's head.</summary>
    public string ColumnAgents => Labels.ProjectsColumnAgents;

    /// <summary>The Blocked on column's head.</summary>
    public string ColumnBlockedOn => Labels.ProjectsColumnBlockedOn;

    /// <summary>The Target column's head.</summary>
    public string ColumnTarget => Labels.ProjectsColumnTarget;

    /// <summary>The Active column's head.</summary>
    public string ColumnActive => Labels.ProjectsColumnActive;
#pragma warning restore CA1822, MA0041

    /// <summary>A view menu or key picked a view: its rows show at once.</summary>
    /// <param name="view">The view.</param>
    internal void SetView(ProjectView view)
    {
        _viewPicked = true;
        ShowView(view);
        Arrange();
    }

    // Until the owner picks a view: Needs me once an open project is served as blocked on the owner; with none blocked (every flag false, or no flag from an
    // older server) the page stays on Projects, so the first view is never empty. A read whose truth failed carries no flag and moves nothing back, so the
    // view never flips between reads.
    private void DefaultView(ProjectList list)
    {
        if (!_viewPicked && !FactsMissing && list.Projects.Any(c => c.Truth?.BlockedOnYou == true))
        {
            ShowView(ProjectView.NeedsMe);
        }
    }

    private void ShowView(ProjectView view)
    {
        View = view;
        foreach (var tab in Tabs)
        {
            tab.IsOn = tab.View == view;
        }

        OnChanged(nameof(ShowsFolds));
    }

    /// <summary>The Sort menu picked a sort.</summary>
    /// <param name="sort">The sort.</param>
    internal void SetSort(ProjectSort sort)
    {
        Sort = sort;
        Mark(SortChoices, (int)sort);
        OnChanged(nameof(SortText));
        OnChanged(nameof(SortName));
        Arrange();
    }

    /// <summary>The Group menu picked a grouping.</summary>
    /// <param name="grouping">The grouping.</param>
    internal void SetGrouping(ProjectGrouping grouping)
    {
        Grouping = grouping;
        Mark(GroupChoices, (int)grouping);
        OnChanged(nameof(GroupText));
        OnChanged(nameof(GroupName));
        OnChanged(nameof(FactsMissingText));
        Arrange();
    }

    /// <summary>A menu's choice was picked (a click, Enter).</summary>
    /// <param name="choice">The choice.</param>
    internal void Choose(ProjectMenuChoice choice)
    {
        if (choice.Menu == ProjectMenu.Sort)
        {
            SetSort((ProjectSort)choice.Value);
        }
        else
        {
            SetGrouping((ProjectGrouping)choice.Value);
        }
    }

    /// <summary>A group head was clicked or got Enter: its rows fold or unfold, and a selection inside a folded group moves to the next row drawn.</summary>
    /// <param name="group">The group.</param>
    internal void ToggleGroup(ProjectGroupViewModel group)
    {
        group.Toggle();
        Reselect();
    }

    /// <summary>The empty view's button: Open Projects, or Back to Needs me on Triage.</summary>
    internal void RunViewEmptyAction() => SetView(View == ProjectView.Triage ? ProjectView.NeedsMe : ProjectView.All);

    private static void Mark(IReadOnlyList<ProjectMenuChoice> choices, int value)
    {
        foreach (var choice in choices)
        {
            choice.IsOn = choice.Value == value;
        }
    }

    // The footer: the field's keys while it has focus, else the row keys, else (no row) the view keys.
    private void ShowKeyHints()
    {
        var hints = _searchFocused ? Labels.ProjectsSearchKeyHints : IsViewEmpty || !HasRows ? Labels.ProjectsEmptyKeyHints : Labels.ProjectsKeyHints;
        KeyHints = [.. hints.Select(h => new ProjectKeyHint(h.Key, h.Word))];
    }
}
