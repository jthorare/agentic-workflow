namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>One panel in the order editor: its Show box and its place.</summary>
/// <param name="id">The panel's name in the API.</param>
/// <param name="visible">True while the panel is shown.</param>
internal sealed class SettingsPanelRow(string id, bool visible) : ObservableObject
{
    /// <summary>The panel's name in the API ("docMap").</summary>
    public string Id { get; } = id;

    /// <summary>The panel in words.</summary>
    public string Label { get; } = Labels.ReviewSettingsPanelWord(id);

    /// <summary>True while the panel is shown; a hidden one is left out of the entry's list.</summary>
    public bool Visible { get; set => Set(ref field, value); } = visible;

    /// <summary>The Show box's name.</summary>
    public string ShowName => Labels.ReviewSettingsShowPanel(Label);

    /// <summary>The move-up button's name.</summary>
    public string UpName => Labels.ReviewSettingsMoveUp(Label);

    /// <summary>The move-down button's name.</summary>
    public string DownName => Labels.ReviewSettingsMoveDown(Label);

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The move-up button's text.</summary>
    public string UpText => Labels.ReviewSettingsUp;

    /// <summary>The move-down button's text.</summary>
    public string DownText => Labels.ReviewSettingsDown;
#pragma warning restore CA1822, MA0041
}
