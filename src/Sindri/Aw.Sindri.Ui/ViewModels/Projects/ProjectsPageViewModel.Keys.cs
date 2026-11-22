namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>
/// The Projects page's keys (<see cref="ProjectsPageViewModel"/>, continued; issue 3717 key map): 1 to 4 show a view, O opens the Sort menu, B the Group menu,
/// J / K (and the arrows, from the view) walk the rows and Enter opens the selected row's sheet. The shell passes them only while Projects is the page and
/// focus is not in the sheet or a field; the app-wide G chord, C and N stay the shell's.
/// </summary>
internal sealed partial class ProjectsPageViewModel
{
    /// <summary>A key asked for the Sort or the Group menu: the view opens it.</summary>
    internal event EventHandler<ProjectMenuRequestedEventArgs>? MenuRequested;

    /// <summary>One single key on the page.</summary>
    /// <param name="key">"1" to "4", "O", "B", "/", "J", "K" or "Enter".</param>
    /// <returns>True when the key was used.</returns>
    internal bool Press(string key) => key switch
    {
        "1" => Viewed(ProjectView.NeedsMe),
        "2" => Viewed(ProjectView.MyWork),
        "3" => Viewed(ProjectView.All),
        "4" => Viewed(ProjectView.Triage),
        "O" => Asked(ProjectMenu.Sort),
        "B" => Asked(ProjectMenu.Group),
        "/" => AskedForSearch(),
        "J" => Move(1),
        "K" => Move(-1),
        "Enter" => Opened(),
        _ => false,
    };

    private bool Viewed(ProjectView view)
    {
        SetView(view);
        return true;
    }

    private bool Asked(ProjectMenu menu)
    {
        MenuRequested?.Invoke(this, new ProjectMenuRequestedEventArgs(menu));
        return true;
    }

    // From no row J goes to the first and K to the last; at either end the selection stays. Focus follows the selection.
    private bool Move(int step)
    {
        var ids = Walk();
        if (ids.Count == 0)
        {
            return false;
        }

        var at = ids.FindIndex(id => string.Equals(id, SelectedId, StringComparison.Ordinal));
        var to = at >= 0 ? Math.Clamp(at + step, 0, ids.Count - 1) : (step > 0 ? 0 : ids.Count - 1);
        SelectedId = ids[to];
        MarkSelected();
        FocusRequested?.Invoke(this, new ProjectFocusEventArgs(ProjectFocus.Card, ids[to]));
        return true;
    }

    // Enter opens the selected row's sheet, the way a click does.
    private bool Opened()
    {
        if (InReadingOrder().FirstOrDefault(c => string.Equals(c.Id, SelectedId, StringComparison.Ordinal)) is not { } card)
        {
            return false;
        }

        RequestOpen(card.Link);
        return true;
    }
}
