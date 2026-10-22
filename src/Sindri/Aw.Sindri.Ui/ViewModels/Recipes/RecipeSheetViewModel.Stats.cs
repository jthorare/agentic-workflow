using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>
/// The recipe sheet's run figures (<see cref="RecipeSheetViewModel"/>, continued; API P12): how often the workflow ran in a window, how many finished, how many
/// needed a person and how long it typically took, for the version shown. A read that fails leaves the sheet as it was, with a line saying so.
/// </summary>
internal sealed partial class RecipeSheetViewModel
{
    private string _shownHash = string.Empty;

    /// <summary>The window the figures cover: '7d', '30d' (the start) or '90d'.</summary>
    public string StatsWindow { get; private set => Set(ref field, value); } = Labels.DefaultStatsWindow;

    /// <summary>The window buttons: one per window the API serves.</summary>
    public IReadOnlyList<WindowChoiceViewModel> StatsWindows { get; } = [.. Labels.StatsWindows.Select(w => new WindowChoiceViewModel(w))];

    /// <summary>The heading over the figures.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string StatsHeading => Labels.StatsHeading;
#pragma warning restore CA1822, MA0041

    /// <summary>The figures, in order; empty before the first answer and when there is a note instead.</summary>
    public IReadOnlyList<RecipeStatLine> StatLines { get; private set => Set(ref field, value); } = [];

    /// <summary>A line instead of the figures (no runs, only runs inside others, or the figures can't be read); empty when the figures show.</summary>
    public string StatsNote { get; private set { Set(ref field, value); OnChanged(nameof(HasStatsNote)); } } = string.Empty;

    /// <summary>True when there is a note.</summary>
    public bool HasStatsNote => StatsNote.Length > 0;

    /// <summary>Chooses the window and reads the figures again at it; a window the API doesn't serve is ignored.</summary>
    /// <param name="window">'7d', '30d' or '90d'.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the figures are shown.</returns>
    internal Task ChooseStatsWindow(string window, CancellationToken cancellationToken)
    {
        if (!Labels.StatsWindows.Contains(window, StringComparer.Ordinal) || string.Equals(window, StatsWindow, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        StatsWindow = window;
        foreach (var choice in StatsWindows)
        {
            choice.IsChosen = string.Equals(choice.Window, window, StringComparison.Ordinal);
        }

        return _url is { } url && Phase == FramePhase.Shown ? ReadStats(url, Split(_subject).Recipe, _generation, cancellationToken) : Task.CompletedTask;
    }

    private async Task ReadStats(Uri url, string recipe, int generation, CancellationToken cancellationToken)
    {
        var window = StatsWindow;
        var answer = await api.RecipeStats(url, recipe, window, _shownHash.Length == 0 ? null : _shownHash, cancellationToken).ConfigureAwait(true);
        if (generation != _generation || !string.Equals(window, StatsWindow, StringComparison.Ordinal))
        {
            return;   // another recipe took over, or another window was chosen since: its answer is the one to show
        }

        if (!answer.IsSuccess)
        {
            (StatLines, StatsNote) = ([], Labels.StatsUnavailable);
            return;
        }

        ShowStats(answer.Value);
    }

    private void ShowStats(RecipeStatsView stats)
    {
        if (stats.ChildOnly)
        {
            (StatLines, StatsNote) = ([], Labels.StatsChildOnlyNote);
        }
        else if ((stats.Runs ?? 0) == 0)
        {
            (StatLines, StatsNote) = ([], Labels.StatsNoRuns);
        }
        else
        {
            (StatLines, StatsNote) = (Lines(stats), string.Empty);
        }
    }

    private static List<RecipeStatLine> Lines(RecipeStatsView stats)
    {
        var runs = stats.Runs ?? 0;
        var lines = new List<RecipeStatLine>
        {
            new(Labels.StatRuns, Labels.StatCount(runs)),
            new(Labels.StatFinished, Labels.StatShare(stats.Done ?? 0, runs)),
            new(Labels.StatHuman, stats.HumanRate is { } rate ? Labels.Percent(rate) : Labels.StatNone),
            new(Labels.StatTypical, stats.MedianSeconds is { } seconds ? Labels.Duration(seconds) : Labels.StatNone),
        };
        AddWhenCounted(lines, Labels.StatFailed, stats.Failed);
        AddWhenCounted(lines, Labels.StatCancelled, stats.Cancelled);
        AddWhenCounted(lines, Labels.StatBudget, stats.BudgetHits);
        if (stats.Tokens?.P50 is { } tokens)
        {
            lines.Add(new RecipeStatLine(Labels.StatTokens, Labels.StatCount(tokens)));
        }

        return lines;
    }

    private static void AddWhenCounted(List<RecipeStatLine> lines, string label, int? count)
    {
        if (count is > 0)
        {
            lines.Add(new RecipeStatLine(label, Labels.StatCount(count.Value)));
        }
    }

    private void ClearStats() => (StatLines, StatsNote, _shownHash) = ([], string.Empty, string.Empty);
}
