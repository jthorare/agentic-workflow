using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.Views.Todo;

/// <summary>The attention area of the Todo panel (todo spec §3.1): a button under a version or a hold pick sends its digit; every word comes from the view model.</summary>
#pragma warning disable MA0182 // why: TodoPanel.axaml places it, which the analyzer cannot see
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
internal sealed partial class AttentionArea : UserControl
{
    public AttentionArea()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnPick);
    }

    // A button's click is its key's pick: 1 is Sindri's side, 2 is Obsidian's (a hold: Put them back, Apply).
    private void OnPick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button { DataContext: AttentionChoiceViewModel choice } && DataContext is TodoPanelViewModel panel
            && int.TryParse(choice.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var digit))
        {
            e.Handled = true;
            _ = panel.Pick(digit);
        }
    }
}
#pragma warning restore MA0182
