using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.NeedsYou;

/// <summary>The sheet of a Needs-you row.</summary>
internal sealed partial class NeedsYouSheet : UserControl
{
    public NeedsYouSheet()
    {
        InitializeComponent();
    }

    // The sheet's Complete is the row's own: it runs the same action the row's button does, through the shell.
    private void OnComplete(object? sender, RoutedEventArgs e)
    {
        if (DataContext is NeedsYouRowViewModel { SheetComplete: { } action } row && TopLevel.GetTopLevel(this)?.DataContext is ShellViewModel shell)
        {
            shell.NeedsYou.Press(row.Id, action);
        }
    }
}
