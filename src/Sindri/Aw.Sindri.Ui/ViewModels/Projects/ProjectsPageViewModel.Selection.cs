namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>
/// The Projects page's selected row (<see cref="ProjectsPageViewModel"/>, continued; issue 3717): the row the keys act on, by project id. A re-read, a new
/// view, sort or grouping keeps it on the same project, and moves it to the row that followed it when that project is no longer drawn.
/// </summary>
internal sealed partial class ProjectsPageViewModel
{
    private List<string> _walked = [];

    /// <summary>The id of the row the keys act on; null for none.</summary>
    public string? SelectedId { get; private set => Set(ref field, value); }

    /// <summary>Selects a row by its project's id (focus arrived on it).</summary>
    /// <param name="id">The project's id.</param>
    /// <returns>True when such a row is drawn.</returns>
    internal bool Select(string id)
    {
        if (!Walk().Contains(id, StringComparer.Ordinal))
        {
            return false;
        }

        SelectedId = id;
        MarkSelected();
        return true;
    }

    // The rows the keys walk, by id, in reading order: the groups, then the folds that are open. A project under two repos is walked once.
    private List<string> Walk() => [.. InReadingOrder().Select(c => c.Id).Distinct(StringComparer.Ordinal)];

    // After a new layout: the selection stays on its project, else moves to the next row that is still drawn, else the one before.
    private void Reselect()
    {
        var now = Walk();
        if (SelectedId is { } at && !now.Contains(at, StringComparer.Ordinal))
        {
            var i = _walked.FindIndex(k => string.Equals(k, at, StringComparison.Ordinal));
            SelectedId = i < 0 ? null : _walked.Skip(i + 1).FirstOrDefault(now.Contains) ?? _walked.Take(i).Reverse().FirstOrDefault(now.Contains);
        }

        _walked = now;
        MarkSelected();
    }

    private void MarkSelected()
    {
        foreach (var card in _cards.Values)
        {
            card.IsSelected = string.Equals(card.Id, SelectedId, StringComparison.Ordinal);
        }
    }
}
