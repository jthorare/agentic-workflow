using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Jobs;

namespace Aw.Sindri.Ui.Views.Jobs;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The job sheet inside the shell's sheet: Copy puts the cancel's terminal form on the clipboard.</summary>
internal sealed partial class JobSheetView : UserControl
{
    public JobSheetView()
    {
        InitializeComponent();
    }

    private void OnCopyId(object? sender, RoutedEventArgs e)
    {
        if (DataContext is JobSheetViewModel sheet)
        {
            _ = sheet.CopyJobId(CancellationToken.None);
        }
    }

    private void OnCopyCli(object? sender, RoutedEventArgs e)
    {
        if (DataContext is JobSheetViewModel sheet)
        {
            _ = sheet.CopyCli(CancellationToken.None);
        }
    }
}
#pragma warning restore MA0182
