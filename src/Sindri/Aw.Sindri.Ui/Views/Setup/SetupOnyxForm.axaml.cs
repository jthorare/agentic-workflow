using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Setup;

namespace Aw.Sindri.Ui.Views.Setup;

#pragma warning disable MA0182 // why: the step row places it in XAML, which the analyzer can't see
/// <summary>The Onyx step's controls: a click, Enter or Esc only calls the view model; the view model asks where focus goes.</summary>
internal sealed partial class SetupOnyxForm : UserControl
{
    private SetupOnyxViewModel? _onyx;

    public SetupOnyxForm()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _onyx?.FocusRequested -= OnFocusRequested;
        _onyx = DataContext as SetupOnyxViewModel;
        _onyx?.FocusRequested += OnFocusRequested;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_onyx is { ShowForm: true })   // a step that was already waiting when the row was drawn
        {
            OnFocusRequested(this, new SetupFocusEventArgs(SetupFocus.OnyxEmail));
        }
    }

    private void OnFocusRequested(object? sender, SetupFocusEventArgs e) =>
        Dispatcher.UIThread.Post(() => (e.Target switch
        {
            SetupFocus.OnyxPassword => (Control)SetupOnyxPassword,
            SetupFocus.OnyxConnect => SetupOnyxOpen,
            SetupFocus.RepoPath or SetupFocus.OnyxEmail or SetupFocus.StepHeading => SetupOnyxEmail,
            _ => SetupOnyxEmail,
        }).Focus(), DispatcherPriority.Input);

    private void OnRevealChanged(object? sender, RoutedEventArgs e)
    {
        if (_onyx is not null && sender is ToggleButton toggle && toggle.IsChecked == true != _onyx.IsRevealed)
        {
            _onyx.ToggleReveal();
        }
    }

    private void OnOpen(object? sender, RoutedEventArgs e) => _onyx?.Open();

#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnStart(object? sender, RoutedEventArgs e)
    {
        if (_onyx is not null)
        {
            await _onyx.Start(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnDefer(object? sender, RoutedEventArgs e)
    {
        if (_onyx is not null)
        {
            await _onyx.Defer(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnConnect(object? sender, RoutedEventArgs e)
    {
        if (_onyx is not null)
        {
            await _onyx.Connect(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnFieldKey(object? sender, KeyEventArgs e)
    {
        if (_onyx is null)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            _onyx.Close();
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await _onyx.Connect(CancellationToken.None).ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155
}
#pragma warning restore MA0182
