using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Jobs;

namespace Aw.Sindri.Ui.Views.Jobs;

/// <summary>The job sheet inside the shell's sheet: Copy puts the cancel's terminal form on the clipboard.</summary>
internal sealed partial class JobSheetView : UserControl
{
    public JobSheetView()
    {
        InitializeComponent();
    }

    private void OnCopyCli(object? sender, RoutedEventArgs e)
    {
        if (DataContext is JobSheetViewModel sheet)
        {
            _ = sheet.CopyCli(CancellationToken.None);
        }
    }
}
