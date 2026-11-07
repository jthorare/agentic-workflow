using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.Views.Linear;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>A Linear issue's sheet inside the shell's sheet: its two buttons call the view model.</summary>
internal sealed partial class LinearIssueSheetView : UserControl
{
    public LinearIssueSheetView()
    {
        InitializeComponent();
    }

    private LinearIssueSheetViewModel? Sheet => DataContext as LinearIssueSheetViewModel;

    private void OnOpenInLinear(object? sender, RoutedEventArgs e) => _ = Sheet?.OpenInLinear(CancellationToken.None);

    private void OnOpenPr(object? sender, RoutedEventArgs e) => Sheet?.OpenPr();
}
