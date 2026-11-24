using Aw.Sindri.Ui.Api.Projects;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>The Projects page's layout (<see cref="ProjectsPageViewModel"/>, continued; issue 3717): laying the last list out into groups, folds, tab counts and the empty state.</summary>
internal sealed partial class ProjectsPageViewModel
{
    // Lays the last list out for the view, sort and grouping in force: the groups, the folds on Projects, the tabs' counts and the empty state.
    private void Arrange()
    {
        if (_list is not { } list)
        {
            return;
        }

        var inView = InViewSorted(list);
        var rows = Searched(inView);
        Patch.To(Groups, BuildGroups(rows));
        ShowFolds(list);
        CountTabs(list);
        HasRows = rows.Count > 0;
        ShowsFactsMissing = FactsMissing;
        ShowEmptyView(empty: !IsEmptyBlock && (View == ProjectView.Triage || (View != ProjectView.All && inView.Count == 0)));
        ShowSearch(inView, rows.Count);
        Reselect();
        Laid?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<ProjectCardView> InViewSorted(ProjectList list) =>
        View == ProjectView.Triage ? [] : ProjectRows.Sorted(list.Projects.Where(c => ProjectRows.InView(View, c)), Sort);

    private List<ProjectGroupViewModel> BuildGroups(IReadOnlyList<ProjectCardView> rows)
    {
        var groups = new List<ProjectGroupViewModel>();
        foreach (var planned in ProjectRows.Grouped(rows, Grouping, FactsMissing))
        {
            var key = $"{Grouping}:{planned.Key}";
            if (!_groups.TryGetValue(key, out var group))
            {
                group = _groups[key] = new ProjectGroupViewModel(planned.Heading);
            }

            group.Hint = Grouping == ProjectGrouping.Health && groups.Count == 0 ? Labels.ProjectsHealthHint : string.Empty;
            group.Show([.. planned.Cards.Select(Card)]);
            groups.Add(group);
        }

        return groups;
    }

    private void ShowFolds(ProjectList list)
    {
        var all = View == ProjectView.All;
        Finished.Show(all ? [.. Searched(ProjectRows.Sorted(list.Finished, Sort)).Select(Card)] : []);
        Hidden.Show(all ? [.. Searched(ProjectRows.Sorted(list.Hidden, Sort)).Select(Card)] : []);
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
                ProjectView.NeedsMe or ProjectView.MyWork => list.Projects.Count(c => ProjectRows.InView(tab.View, c)),
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
            ProjectView.Triage or ProjectView.All => (Labels.ProjectsTriageTitle, Labels.ProjectsTriageBody, Labels.ProjectsBackToNeedsMe, "1"),
            _ => (Labels.ProjectsTriageTitle, Labels.ProjectsTriageBody, Labels.ProjectsBackToNeedsMe, "1"),
        };
        ShowKeyHints();
    }
}
