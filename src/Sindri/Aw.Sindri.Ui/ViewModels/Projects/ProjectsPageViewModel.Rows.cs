using System.Globalization;
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

    /// <summary>The view shown. Projects (every open project) until the owner picks another: Needs me reads a server flag that is not served yet (issue 3797).</summary>
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

    /// <summary>True when the list draws the line that the facts are missing: they are, and the view has rows.</summary>
    public bool ShowsFactsMissing { get; private set => Set(ref field, value); }

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

    /// <summary>The line drawn instead of health groups when the facts are missing.</summary>
    public string FactsMissingText => Labels.ProjectSindriFactsMissing;

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
        View = view;
        foreach (var tab in Tabs)
        {
            tab.IsOn = tab.View == view;
        }

        OnChanged(nameof(ShowsFolds));
        Arrange();
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

    /// <summary>The empty view's button: Open Projects, or Back to Needs me on Triage.</summary>
    internal void RunViewEmptyAction() => SetView(View == ProjectView.Triage ? ProjectView.NeedsMe : ProjectView.All);

    private static void Mark(IReadOnlyList<ProjectMenuChoice> choices, int value)
    {
        foreach (var choice in choices)
        {
            choice.IsOn = choice.Value == value;
        }
    }

    // Lays the last list out for the view, sort and grouping in force: the groups, the folds on Projects, the tabs' counts and the empty state.
    private void Arrange()
    {
        if (_list is not { } list)
        {
            return;
        }

        IReadOnlyList<ProjectCardView> rows = View == ProjectView.Triage ? [] : ProjectRows.Sorted(list.Projects.Where(c => ProjectRows.InView(View, c)), Sort);
        var groups = new List<ProjectGroupViewModel>();
        foreach (var planned in ProjectRows.Grouped(rows, Grouping, FactsMissing))
        {
            var key = string.Create(CultureInfo.InvariantCulture, $"{Grouping}:{planned.Key}");
            if (!_groups.TryGetValue(key, out var group))
            {
                group = _groups[key] = new ProjectGroupViewModel(planned.Heading);
            }

            group.Hint = Grouping == ProjectGrouping.Health && groups.Count == 0 ? Labels.ProjectsHealthHint : string.Empty;
            group.Show([.. planned.Cards.Select(Card)]);
            groups.Add(group);
        }

        Patch.To(Groups, groups);
        var all = View == ProjectView.All;
        Finished.Show(all ? [.. ProjectRows.Sorted(list.Finished, Sort).Select(Card)] : []);
        Hidden.Show(all ? [.. ProjectRows.Sorted(list.Hidden, Sort).Select(Card)] : []);
        CountTabs(list);
        HasRows = rows.Count > 0;
        ShowsFactsMissing = FactsMissing && HasRows;
        ShowEmptyView(empty: !IsEmptyBlock && (View == ProjectView.Triage || (View != ProjectView.All && rows.Count == 0)));
        Reselect();
    }

    private void CountTabs(ProjectList list)
    {
        foreach (var tab in Tabs)
        {
            tab.ShowsCount = tab.View != ProjectView.NeedsMe || !FactsMissing;
            tab.Count = tab.View switch
            {
                ProjectView.All => list.Projects.Count + list.Finished.Count,   // the cards the answer holds, open and finished; hidden ones are not counted
                ProjectView.Triage => 0,
                _ => list.Projects.Count(c => ProjectRows.InView(tab.View, c)),
            };
        }
    }

    private void ShowEmptyView(bool empty)
    {
        IsViewEmpty = empty;
        (ViewEmptyTitle, ViewEmptyBody, ViewEmptyAction, ViewEmptyKey) = !empty ? (string.Empty, string.Empty, string.Empty, string.Empty) : View switch
        {
            ProjectView.NeedsMe => (Labels.ProjectsNeedsMeEmptyTitle, Labels.ProjectsNeedsMeEmptyBody, Labels.ProjectsOpenAll, "3"),
            ProjectView.MyWork => (Labels.ProjectsMyWorkEmptyTitle, Labels.ProjectsMyWorkEmptyBody, Labels.ProjectsOpenAll, "3"),
            _ => (Labels.ProjectsTriageTitle, Labels.ProjectsTriageBody, Labels.ProjectsBackToNeedsMe, "1"),
        };
        var hints = empty || !HasRows ? Labels.ProjectsEmptyKeyHints : Labels.ProjectsKeyHints;
        KeyHints = [.. hints.Select(h => new ProjectKeyHint(h.Key, h.Word))];
    }
}
