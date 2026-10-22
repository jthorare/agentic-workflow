namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>The Recipes page's window (<see cref="RecipesPageViewModel"/>, continued): the days the run figures cover, 7, 30 or 90.</summary>
internal sealed partial class RecipesPageViewModel
{
    /// <summary>The window the figures cover: '7d', '30d' (the start) or '90d'.</summary>
    public string Window { get; private set => Set(ref field, value); } = Labels.DefaultStatsWindow;

    /// <summary>The window buttons: one per window the API serves.</summary>
    public IReadOnlyList<WindowChoiceViewModel> Windows { get; } = [.. Labels.StatsWindows.Select(w => new WindowChoiceViewModel(w))];

    /// <summary>The Runs column's header.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string ColRuns => Labels.ColRuns;

    /// <summary>The Finished column's header.</summary>
    public string ColFinished => Labels.ColFinished;

    /// <summary>The Typical time column's header.</summary>
    public string ColTypical => Labels.ColTypical;
#pragma warning restore CA1822, MA0041

    /// <summary>Chooses the window and reads the list again at it; a window the API doesn't serve is ignored.</summary>
    /// <param name="window">'7d', '30d' or '90d'.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown (at once when the page is not the one shown: it reads when it is).</returns>
    internal Task ChooseWindow(string window, CancellationToken cancellationToken)
    {
        if (!Labels.StatsWindows.Contains(window, StringComparer.Ordinal) || string.Equals(window, Window, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        Window = window;
        foreach (var choice in Windows)
        {
            choice.IsChosen = string.Equals(choice.Window, window, StringComparison.Ordinal);
        }

        return IsActive && _url is { } url ? Read(url, cancellationToken) : Task.CompletedTask;
    }
}
