using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The shell shows Settings filtered to one setting for a palette entry (#3362).</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>Raised with the filter words when the palette asked for one setting; the Settings page sets its filter box to them.</summary>
    internal event EventHandler<ValueEventArgs<string>>? SettingFilterRequested;

    private void OpenSettingFilter(string words)
    {
        GoTo(PageId.Settings);
        SettingFilterRequested?.Invoke(this, new ValueEventArgs<string>(words));
    }
}
