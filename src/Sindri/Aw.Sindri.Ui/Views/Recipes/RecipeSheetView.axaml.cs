using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Recipes;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Recipes;

/// <summary>The recipe sheet inside the shell's sheet: Back returns to the frame or sheet this one was opened from, else closes the sheet like Esc.</summary>
internal sealed partial class RecipeSheetView : UserControl
{
    public RecipeSheetView()
    {
        InitializeComponent();
    }

    private void OnBack(object? sender, RoutedEventArgs e) => (TopLevel.GetTopLevel(this)?.DataContext as ShellViewModel)?.Back();

#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnStatsWindow(object? sender, RoutedEventArgs e)
    {
        if (DataContext is RecipeSheetViewModel sheet && sender is Control { DataContext: WindowChoiceViewModel choice })
        {
            await sheet.ChooseStatsWindow(choice.Window, CancellationToken.None).ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155

    private void OnCopyApprove(object? sender, RoutedEventArgs e) => _ = (DataContext as RecipeSheetViewModel)?.CopyApprove(CancellationToken.None);
}
