using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Todo;

/// <summary>One todo row of the panel on Home (todo spec §3.1, §3.5); the view model carries every word.</summary>
#pragma warning disable MA0182 // why: TodoPanel.axaml places it in a DataTemplate, which the analyzer cannot see
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
internal sealed partial class TodoRow : UserControl
{
    public TodoRow()
    {
        InitializeComponent();
    }
}
#pragma warning restore MA0182
