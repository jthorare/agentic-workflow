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

    // Under about 80 px a TRIMMED reason would show a few letters and an ellipsis: it goes whole (still laid out, so it comes back when the cell grows) and the health dot says the level.
    // A reason that fits whole stays, however narrow its column (issue 4186).
    private const double MinReasonWidth = 80;

    // What the name leaves for the reason column when the name wants all of the Project cell: the column's 10 px margin and the dot with its 6 px gap (8 + 6). The name's
    // MaxWidth is the cell minus this, so a long name trims and the dot stays inside the cell (issue 4186).
    private const double DotReserve = 24;

    public ProjectRow()
    {
        InitializeComponent();
        AddHandler(PointerReleasedEvent, OnPointerReleased);
        ProjectRowReason.SizeChanged += OnReasonSized;
        ProjectRowTitle.SizeChanged += OnTitleSized;
    }

    private void OnReasonSized(object? sender, SizeChangedEventArgs e) =>
        ProjectRowReason.Opacity = e.NewSize.Width < MinReasonWidth && ProjectRowReason.TextLayout.TextLines.Any(static l => l.HasCollapsed) ? 0 : 1;

    private void OnTitleSized(object? sender, SizeChangedEventArgs e)
    {
        var max = Math.Max(0, e.NewSize.Width - DotReserve);
        if (!double.IsFinite(ProjectRowName.MaxWidth) || Math.Abs(ProjectRowName.MaxWidth - max) > 0.5)
        {
            ProjectRowName.MaxWidth = max;
        }
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
