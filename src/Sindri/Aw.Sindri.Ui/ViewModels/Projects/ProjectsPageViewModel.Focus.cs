namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>The Projects page (<see cref="ProjectsPageViewModel"/>, continued): where focus goes after a write took a card off the page (or back).</summary>
internal sealed partial class ProjectsPageViewModel
{
    /// <summary>Re-reads the list after a write took a card off the page (or back), then asks the view to move focus: to the next card, else the previous, else Add project; an unhidden project's fold.</summary>
    /// <param name="done">What the write did.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown and focus is asked for.</returns>
    internal async Task Settled(ProjectDoneEventArgs done, CancellationToken cancellationToken)
    {
        var order = InReadingOrder().Select(c => c.Id).ToList();
        var at = order.IndexOf(done.Id);
        var candidates = at < 0 ? [] : order.Skip(at + 1).Take(1).Concat(order.Take(at).Reverse().Take(1)).ToList();   // the next card, else the previous
        if (_url is { } url)
        {
            await Read(url, cancellationToken).ConfigureAwait(true);
        }

        FocusRequested?.Invoke(this, done.Kind switch
        {
            ProjectDone.Unhidden => Fold(done.WentToFinished),
            ProjectDone.Added => Card([done.Id], gone: string.Empty),   // the new card, else Add project when a fold hides it
            ProjectDone.Removed or ProjectDone.Hidden => Card(candidates, done.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(done), done.Kind, message: null),
        });
    }

    // The cards as the page lays them out: the groups, then the folds that are open.
    private IEnumerable<ProjectCardViewModel> InReadingOrder() =>
        Groups.Where(g => g.IsExpanded).SelectMany(g => g.Cards).Concat(Finished.IsOpen ? Finished.Cards : []).Concat(Hidden.IsOpen ? Hidden.Cards : []);

    // The first neighbour still on the page, else Add project.
    private ProjectFocusEventArgs Card(List<string> candidates, string gone) =>
        candidates.FirstOrDefault(id => !string.Equals(id, gone, StringComparison.Ordinal) && InReadingOrder().Any(c => string.Equals(c.Id, id, StringComparison.Ordinal))) is { } id
            ? new ProjectFocusEventArgs(ProjectFocus.Card, id)
            : new ProjectFocusEventArgs(ProjectFocus.Add, cardId: null);

    // An unhidden project's fold button, while it still has cards, else Add project.
    private ProjectFocusEventArgs Fold(bool finished) =>
        (finished ? Finished : Hidden).IsVisible
            ? new ProjectFocusEventArgs(finished ? ProjectFocus.FinishedFold : ProjectFocus.HiddenFold, cardId: null)
            : new ProjectFocusEventArgs(ProjectFocus.Add, cardId: null);
}
