using Avalonia.Controls;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Views.Projects;

/// <summary>The Projects page: it tells its view model its own width, which sets how many cards sit across.</summary>
internal sealed partial class ProjectsPage : UserControl
{
    public ProjectsPage()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Resize(e.NewSize.Width);
        DataContextChanged += (_, _) => Resize(Bounds.Width);
    }

    private void Resize(double width)
    {
        if (DataContext is ProjectsPageViewModel page && width > 0)
        {
            page.SetPageWidth(width);
        }
    }
}
