using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.Views.Instance;

/// <summary>
/// The expanded state: it routes presses to its view model. Cancel's confirm takes focus on Keep, the safe answer, and either
/// answer gives focus back to Cancel (or, once the confirm is gone, to Copy status) so focus is never lost.
/// </summary>
internal sealed partial class ExpandedStateView : UserControl
{
    private ExpandedStateViewModel? _state;

    public ExpandedStateView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _state?.PropertyChanged -= OnStateChanged;
        _state = DataContext as ExpandedStateViewModel;
        _state?.PropertyChanged += OnStateChanged;
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ExpandedStateViewModel.IsConfirmingCancel), StringComparison.Ordinal))
        {
            Dispatcher.UIThread.Post(MoveFocus, DispatcherPriority.Loaded);
        }
    }

    private void MoveFocus()
    {
        if (_state is null)
        {
            return;
        }

        var target = _state.IsConfirmingCancel ? this.FindControl<Button>("ExpandedKeep") : AskButton() ?? this.FindControl<Button>("ExpandedCopyStatus");
        _ = target?.Focus(NavigationMethod.Tab);
    }

    private Button? AskButton() => this.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => string.Equals(b.Name, "CommandAsk", StringComparison.Ordinal) && b.IsEffectivelyVisible);

    private void OnCopyStatus(object? sender, RoutedEventArgs e) => _ = _state?.CopyStatus(CancellationToken.None);

    private void OnCopyCommand(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ExpandedCommandViewModel command })
        {
            _ = command.Copy(CancellationToken.None);
        }
    }

    private void OnCopyAttach(object? sender, RoutedEventArgs e) => _ = _state?.CopyAttachCommand(CancellationToken.None);

    private void OnAnswerInChild(object? sender, RoutedEventArgs e) => _state?.AnswerInTheChild();

    private void OnSession(object? sender, RoutedEventArgs e) => _state?.OpenSession();

    private void OnChild(object? sender, RoutedEventArgs e) => _state?.Child?.Open();

    private void OnAsk(object? sender, RoutedEventArgs e) => _state?.AskCancel();

    private void OnHandBack(object? sender, RoutedEventArgs e) => _ = _state?.HandBackAsync();

    private void OnKeep(object? sender, RoutedEventArgs e) => _state?.Keep();

    private void OnConfirmCancel(object? sender, RoutedEventArgs e) => _state?.ConfirmCancel();
}
