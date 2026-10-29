using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Views.Projects;

#pragma warning disable MA0182 // why: ProjectsPage.axaml places it in a data template, which the analyzer cannot see
/// <summary>One project's card on the Projects page.</summary>
internal sealed partial class ProjectCard : UserControl
{
    public ProjectCard()
    {
        InitializeComponent();
        // The boundary of the notes area: Enter, Space and a press are the notes area's own (a newline, a space, a click into the box), so they stop here and
        // the card around it, which opens its sheet on Enter or a click, never sees them. Tab and the arrows pass: Tab is how focus reaches Save and Cancel.
        ProjectNotesArea.AddHandler(KeyDownEvent, StopCardKeys, RoutingStrategies.Bubble, handledEventsToo: true);
        ProjectNotesArea.AddHandler(PointerPressedEvent, StopCardPointer, RoutingStrategies.Bubble, handledEventsToo: true);
        ProjectNotesArea.AddHandler(TappedEvent, StopCardPointer, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private static void StopCardKeys(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
        }
    }

    private static void StopCardPointer(object? sender, RoutedEventArgs e) => e.Handled = true;

    private void OnEdit(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ProjectCardViewModel card)
        {
            card.BeginEdit();
            _ = ProjectNotesBox.Focus();
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => (DataContext as ProjectCardViewModel)?.CancelEdit();

    private void OnSave(object? sender, RoutedEventArgs e) => _ = (DataContext as ProjectCardViewModel)?.Save(CancellationToken.None);

    private void OnReload(object? sender, RoutedEventArgs e) => _ = (DataContext as ProjectCardViewModel)?.Reload(CancellationToken.None);
}
#pragma warning restore MA0182
