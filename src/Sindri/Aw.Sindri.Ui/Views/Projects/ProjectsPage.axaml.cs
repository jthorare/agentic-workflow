using Avalonia;
using Avalonia.Controls;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Views.Projects;

/// <summary>The Projects page: it tells its view model its own width, which sets how many cards sit across.</summary>
internal sealed partial class ProjectsPage : UserControl
{
    /// <summary>How many cards sit across; bound to the view model's Columns, read by the card grids below.</summary>
    internal static readonly StyledProperty<int> ColumnsProperty = AvaloniaProperty.Register<ProjectsPage, int>(nameof(Columns), 1);

    public ProjectsPage()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Resize(e.NewSize.Width);
        DataContextChanged += (_, _) => Resize(Bounds.Width);
    }

    /// <summary>How many cards sit across.</summary>
    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    private void Resize(double width)
    {
        if (DataContext is ProjectsPageViewModel page && width > 0)
        {
            page.SetPageWidth(width);
        }
    }
}
