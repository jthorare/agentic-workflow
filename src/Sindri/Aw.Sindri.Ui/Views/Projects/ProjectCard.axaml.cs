using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Aw.Sindri.Ui.Views.Projects;

#pragma warning disable MA0182 // why: ProjectsPage.axaml places it in a data template, which the analyzer cannot see
/// <summary>One project's card on the Projects page: a click or Enter on it is passed up to the page as a routed event, which opens its sheet.</summary>
internal sealed partial class ProjectCard : UserControl
{
    /// <summary>A click or Enter: open the project's sheet.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenEvent = RoutedEvent.Register<ProjectCard, RoutedEventArgs>(nameof(OpenEvent), RoutingStrategies.Bubble);

    public ProjectCard()
    {
        InitializeComponent();
        AddHandler(PointerReleasedEvent, OnPointerReleased);
        AddHandler(KeyDownEvent, OnKeyDown);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left && new Avalonia.Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            RaiseEvent(new RoutedEventArgs(OpenEvent, this));
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e is { Key: Key.Enter, KeyModifiers: KeyModifiers.None })
        {
            e.Handled = true;
            RaiseEvent(new RoutedEventArgs(OpenEvent, this));
        }
    }
}
#pragma warning restore MA0182
