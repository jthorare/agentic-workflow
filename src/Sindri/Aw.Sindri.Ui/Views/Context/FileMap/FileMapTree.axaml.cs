using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.Views.Context.FileMap;

/// <summary>The Files view: one folder of the repo as 36 px rows. A folder's name opens it; a step of the breadcrumb goes back to that level.</summary>
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

    private void OnCrumb(object? sender, ValueEventArgs<Crumb> e)
    {
        if (DataContext is FileMapViewModel files)
        {
            _ = files.Go(e.Value);
        }
    }
}
