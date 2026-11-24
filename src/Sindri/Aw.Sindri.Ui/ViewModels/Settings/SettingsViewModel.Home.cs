using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Settings;

/// <summary>Settings' "Home as rows" switch (<see cref="SettingsViewModel"/>, continued; home spec §7.1, U1): Home as one ranked list in place of its panels, kept in the preferences.</summary>
internal sealed partial class SettingsViewModel
{
    /// <summary>The switch's label.</summary>
    public string HomeAsRowsLabel { get; } = Labels.HomeAsRows;

    /// <summary>The line under the label: what the switch changes.</summary>
    public string HomeAsRowsNote { get; } = Labels.HomeAsRowsNote;

    /// <summary>True when Home shows as rows; false shows its panels.</summary>
    public bool HomeAsRows
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnChanged(nameof(HomeAsRows));
            if (!_loading)
            {
                _ = _preferences.Update(p => p with { HomeLayout = value ? UiPreferences.HomeRows : UiPreferences.HomePanels });
            }
        }
    }
}
