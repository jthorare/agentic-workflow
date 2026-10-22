using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Context;

namespace Aw.Sindri.Ui.Views.Context.FileMap;

/// <summary>The Files view: one folder of the repo as 36 px rows. A folder's name opens it; Up goes back one level.</summary>
internal sealed partial class FileMapTree : UserControl
{
    public FileMapTree()
    {
        InitializeComponent();
    }

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FileMapViewModel files && sender is Control { DataContext: FileRowViewModel row })
        {
            _ = files.Open(row);
        }
    }

    private void OnUp(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FileMapViewModel files)
        {
            _ = files.Up();
        }
    }
}
