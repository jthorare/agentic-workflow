using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Workflows;

#pragma warning disable MA0182 // why: WorkflowsPage.axaml places it, which the analyzer cannot see
/// <summary>The findings about the whole workflow (#4380): view plumbing only.</summary>
internal sealed partial class WorkflowLevelFindingsList : UserControl
{
    public WorkflowLevelFindingsList()
    {
        InitializeComponent();
    }
}
#pragma warning restore MA0182
