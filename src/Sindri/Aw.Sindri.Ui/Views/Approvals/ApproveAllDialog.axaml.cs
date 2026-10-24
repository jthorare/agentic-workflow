using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Approvals;

namespace Aw.Sindri.Ui.Views.Approvals;

/// <summary>
/// The approve-all dialog: one Approve click answers; Esc and Close shut it with nothing sent.
/// </summary>
internal sealed partial class ApproveAllDialog : UserControl
{
    private INotifyPropertyChanged? _watched;

    public ApproveAllDialog()
    {
        InitializeComponent();
    }

    /// <summary>Moves focus to the Approve button, or to Close when there is nothing to approve (the dialog just opened).</summary>
    internal void FocusFirst()
    {
        var target = this.Get<Button>(DataContext is ApproveAllViewModel { CanApprove: true } ? "ApproveButton" : "ApproveClose");
        _ = target.Focus(NavigationMethod.Tab);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _watched?.PropertyChanged -= OnModelChanged;
        _watched = DataContext as INotifyPropertyChanged;
        _watched?.PropertyChanged += OnModelChanged;
    }

    // Focus follows what the dialog is waiting for: the Approve button when the plan shows, Close (or Done) when the approval runs or ends.
    // It moves only from nowhere, from Close, or from a control that just left the screen, never away from where the person is typing or reading.
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ApproveAllViewModel dialog)
        {
            return;
        }

        var changed = e.PropertyName ?? string.Empty;
        if (string.Equals(changed, nameof(ApproveAllViewModel.CanApprove), StringComparison.Ordinal) && dialog.CanApprove)
        {
            Dispatcher.UIThread.Post(() => MoveFocusTo("ApproveButton"), DispatcherPriority.Loaded);
        }
        else if (string.Equals(changed, nameof(ApproveAllViewModel.Phase), StringComparison.Ordinal) && dialog.Phase is ApproveAllPhase.Applying or ApproveAllPhase.Done or ApproveAllPhase.Failed)
        {
            Dispatcher.UIThread.Post(() => MoveFocusTo("ApproveClose"), DispatcherPriority.Loaded);
        }
    }

    private void MoveFocusTo(string name)
    {
        var target = this.Get<Button>(name);
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        if (target.IsEffectivelyVisible && (focused is null || !focused.IsEffectivelyVisible || ReferenceEquals(focused, this.Get<Button>("ApproveClose"))))
        {
            _ = target.Focus(NavigationMethod.Tab);
        }
    }

    private void OnApprove(object? sender, RoutedEventArgs e) => _ = (DataContext as ApproveAllViewModel)?.Approve();

    private void OnClose(object? sender, RoutedEventArgs e) => (DataContext as ApproveAllViewModel)?.Close();

    private void OnScrim(object? sender, PointerPressedEventArgs e) => (DataContext as ApproveAllViewModel)?.Close();

    private void OnCopy(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ApproveAllViewModel dialog && sender is Control { DataContext: ApproveAllLine { Cli: var cli } })
        {
            _ = dialog.Copy(cli);
        }
    }
}
