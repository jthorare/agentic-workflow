using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.Views.Todo;

/// <summary>One todo row of the panel on Home (todo spec §3.1, §3.5); the view model carries every word. When the row is an edit, Enter saves and Esc cancels, caught before the field sees them.</summary>
#pragma warning disable MA0182 // why: TodoPanel.axaml places it in a DataTemplate, which the analyzer cannot see
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
internal sealed partial class TodoRow : UserControl
{
    private TodoRowViewModel? _row;

    public TodoRow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        DataContextChanged += OnDataContext;
    }

    private void OnDataContext(object? sender, EventArgs e)
    {
        _row?.PropertyChanged -= OnRowChanged;
        _row = DataContext as TodoRowViewModel;
        _row?.PropertyChanged += OnRowChanged;
    }

    // The edit appeared: the text field takes focus once the row has drawn it.
    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(TodoRowViewModel.Edit), StringComparison.Ordinal) && _row?.Edit is not null)
        {
            Dispatcher.UIThread.Post(() => TodoEditText.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
        }
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (_row?.Edit is not { } edit || e.Source is not TextBox || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            _ = edit.Save();
        }
        else if (e.Key == Key.Escape)
        {
            edit.Cancel();
        }
        else
        {
            return;
        }

        e.Handled = true;
    }
}
#pragma warning restore MA0182
