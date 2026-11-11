using Avalonia.Interactivity;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The Jobs page and the window (<see cref="ShellWindow"/>, continued): System's link goes to it.</summary>
internal sealed partial class ShellWindow
{
    private void OnJobsLink(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.GoTo(PageId.Jobs);

    // Memory is not a tab either: System's link goes to it.
    private void OnMemoryLink(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.GoTo(PageId.Memory);

    // Resources is not a tab either: System's link goes to it.
    private void OnResourcesLink(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.GoTo(PageId.Resources);
}
