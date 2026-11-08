using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.ViewModels.Toast;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The shell's toast slot (<see cref="ShellViewModel"/>, continued; todo spec §3.3): later slices call <see cref="ShowToast"/>.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The one toast slot, drawn over the page's bottom.</summary>
    public ToastHostViewModel Toast { get; private set; }

    /// <summary>Shows a toast: it replaces an older one, or waits behind a pick's open window.</summary>
    /// <param name="toast">The toast.</param>
    internal void ShowToast(ToastMessage toast) => Toast.Show(toast);

    [MemberNotNull(nameof(Toast))]
    private void InitToast() => Toast = new ToastHostViewModel(_clock);
}
