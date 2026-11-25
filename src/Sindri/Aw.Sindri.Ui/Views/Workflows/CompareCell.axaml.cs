using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Workflows;

#pragma warning disable MA0182 // why: WorkflowsPage.axaml places it, which the analyzer cannot see
/// <summary>One run's side of a Compare step: status and duration, the body lines and the message, with what differs marked.</summary>
internal sealed partial class CompareCell : UserControl
{
    public CompareCell()
    {
        InitializeComponent();
    }
}
#pragma warning restore MA0182
