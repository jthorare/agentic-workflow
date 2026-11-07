namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>Which panels the owner collapsed, kept per settings name (review page spec §6.2): the next PR of the same settings opens the same way.</summary>
internal sealed class ReviewPanelMemory
{
    private readonly Dictionary<string, Dictionary<string, bool>> _bySettings = new(StringComparer.Ordinal);

    /// <summary>What the owner chose for a panel under a settings name.</summary>
    /// <param name="settings">The settings name.</param>
    /// <param name="panel">The panel id.</param>
    /// <returns>True collapsed, false open; null when the owner never chose (the layout's default applies).</returns>
    internal bool? Chosen(string settings, string panel) =>
        _bySettings.TryGetValue(settings, out var panels) && panels.TryGetValue(panel, out var collapsed) ? collapsed : null;

    /// <summary>Keeps the owner's choice.</summary>
    /// <param name="settings">The settings name.</param>
    /// <param name="panel">The panel id.</param>
    /// <param name="collapsed">True collapsed.</param>
    internal void Choose(string settings, string panel, bool collapsed)
    {
        if (!_bySettings.TryGetValue(settings, out var panels))
        {
            _bySettings[settings] = panels = new Dictionary<string, bool>(StringComparer.Ordinal);
        }

        panels[panel] = collapsed;
    }
}
