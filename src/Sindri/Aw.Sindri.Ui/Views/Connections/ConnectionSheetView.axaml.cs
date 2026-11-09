using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Connections;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The connection sheet inside the shell's sheet: read-only, with its row's actions.</summary>
internal sealed partial class ConnectionSheetView : UserControl
{
    public ConnectionSheetView()
    {
        InitializeComponent();
    }
}
#pragma warning restore MA0182
