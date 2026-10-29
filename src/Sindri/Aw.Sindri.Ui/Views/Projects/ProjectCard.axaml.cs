using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Projects;

#pragma warning disable MA0182 // why: ProjectsPage.axaml places it in a data template, which the analyzer cannot see
/// <summary>One project's card on the Projects page.</summary>
internal sealed partial class ProjectCard : UserControl
{
    public ProjectCard()
    {
        InitializeComponent();
    }
}
#pragma warning restore MA0182
