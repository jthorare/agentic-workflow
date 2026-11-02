using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.NeedsYou;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The sheet of a Needs-you row.</summary>
internal sealed partial class NeedsYouSheet : UserControl
{
    public NeedsYouSheet()
    {
        InitializeComponent();
    }

    // The sheet's Complete is the row's own: it runs the same action the row's button does, through the shell; with the files the sheet's inputs hold when the task needs outputs.
    private void OnComplete(object? sender, RoutedEventArgs e)
    {
        if (DataContext is NeedsYouRowViewModel { SheetComplete: not null } row && TopLevel.GetTopLevel(this)?.DataContext is ShellViewModel shell)
        {
            _ = shell.CompleteSheetRow(row);
        }
    }

    private void OnBrowseOutput(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: DeskOutputViewModel output })
        {
            _ = output.Browse(CancellationToken.None);
        }
    }
}
#pragma warning restore MA0182
