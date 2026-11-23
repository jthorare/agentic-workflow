using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Aw.Sindri.Ui.Views.Projects;

#pragma warning disable MA0182 // why: ProjectsPage.axaml places it in a data template, which the analyzer cannot see
/// <summary>One project's row on the Projects page: a click on it is passed up to the page as a routed event, which opens its sheet. Enter and the arrow keys are the page's.</summary>
internal sealed partial class ProjectRow : UserControl
{
    /// <summary>A click: open the project's sheet.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenEvent = RoutedEvent.Register<ProjectRow, RoutedEventArgs>(nameof(OpenEvent), RoutingStrategies.Bubble);

    public ProjectRow()
    {
        InitializeComponent();
        AddHandler(PointerReleasedEvent, OnPointerReleased);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left && new Avalonia.Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            RaiseEvent(new RoutedEventArgs(OpenEvent, this));
        }
    }
}
#pragma warning restore MA0182
