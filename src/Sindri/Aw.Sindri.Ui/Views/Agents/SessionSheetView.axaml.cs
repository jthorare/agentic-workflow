using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Views.Agents;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The session sheet inside the shell's sheet: Copy puts the pending Focus's CLI on the clipboard.</summary>
internal sealed partial class SessionSheetView : UserControl
{
    public SessionSheetView()
    {
        InitializeComponent();
    }

    private void OnOpenFrame(object? sender, RoutedEventArgs e) => (DataContext as SessionSheetViewModel)?.OpenFrame();

    private void OnCopyRef(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SessionSheetViewModel sheet)
        {
            _ = sheet.CopyRef(CancellationToken.None);
        }
    }

    private void OnCopyFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SessionSheetViewModel sheet)
        {
            _ = sheet.CopyFocus(CancellationToken.None);
        }
    }
}
#pragma warning restore MA0182
