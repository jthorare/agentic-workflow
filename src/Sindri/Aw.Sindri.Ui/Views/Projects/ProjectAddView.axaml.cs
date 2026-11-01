using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Views.Projects;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The Add project sheet inside the shell's sheet: its tabs and buttons call the view model.</summary>
internal sealed partial class ProjectAddView : UserControl
{
    public ProjectAddView()
    {
        InitializeComponent();
    }

    private ProjectAddViewModel? Sheet => DataContext as ProjectAddViewModel;

    private void OnFromLinear(object? sender, RoutedEventArgs e)
    {
        Sheet?.SelectFromLinear();
        Keep(sender);
    }

    private void OnByHand(object? sender, RoutedEventArgs e)
    {
        Sheet?.SelectByHand();
        Keep(sender);
    }

    // A toggle flips itself on a click; the tab shown is the view model's, so a click on the tab already on leaves it on.
    private void Keep(object? sender)
    {
        if (sender is ToggleButton { Name: { } name } button)
        {
            button.IsChecked = string.Equals(name, "ProjectAddTabLinear", StringComparison.Ordinal) ? Sheet?.IsFromLinear : Sheet?.IsByHand;
        }
    }

    private void OnAttach(object? sender, RoutedEventArgs e) => ((sender as Button)?.DataContext as ProjectLinearRowViewModel)?.RequestAttach();

    private void OnSave(object? sender, RoutedEventArgs e) => _ = Sheet?.Save(CancellationToken.None);

    private void OnCancel(object? sender, RoutedEventArgs e) => Sheet?.Cancel();
}
#pragma warning restore MA0182
