namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>One window button (7, 30 or 90 days) on the Recipes page or its sheet.</summary>
/// <param name="window">'7d', '30d' or '90d'.</param>
internal sealed class WindowChoiceViewModel(string window) : ObservableObject
{
    /// <summary>The window as the API spells it.</summary>
    public string Window { get; } = window;

    /// <summary>The button's words.</summary>
    public string Text { get; } = Labels.StatsWindowButton(window);

    /// <summary>True while it is the window chosen; the start window is chosen.</summary>
    public bool IsChosen { get; internal set => Set(ref field, value); } = string.Equals(window, Labels.DefaultStatsWindow, StringComparison.Ordinal);
}
