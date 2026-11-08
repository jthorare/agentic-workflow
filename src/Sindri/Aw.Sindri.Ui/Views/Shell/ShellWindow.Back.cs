using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The sheet's Back and where focus goes when the sheet closes (<see cref="ShellWindow"/>, continued; UI spec §11.2).</summary>
internal sealed partial class ShellWindow
{
    // The sheet header's Back: closes the sheet like Esc does, after the question a Linear issue with unsaved edits asks.
    private void OnBack(object? sender, RoutedEventArgs e) => Shell?.Back();

    // Where focus goes when the sheet closes: the session, project, library or queue row that opened it, the edge tab, else the row the selection moved to.
    private Control? SheetOpenerFrame() => OpenerSessionFrame() ?? OpenerProjectFrame() ?? OpenerLibraryButton() ?? OpenerRowFrame() ?? Usable(_sheetOpener) ?? SelectedRowFrame();
}
