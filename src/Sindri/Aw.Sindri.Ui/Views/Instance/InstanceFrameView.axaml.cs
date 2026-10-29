using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Facts;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Instance;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The instance frame inside the sheet: Back closes the sheet like Esc, and Open in hands the subject's link to the platform.</summary>
internal sealed partial class InstanceFrameView : UserControl
{
    private const int MaxLooks = 20;

    private InstanceFrameViewModel? _frame;
    private FrameFocusEventArgs? _wanted;
    private int _attempts;

    public InstanceFrameView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    // Esc with a Why flyout open closes the flyout only and puts focus back on the sentence that opened it; the sheet stays, and the next Esc closes it
    // (model-pick spec section 6.1, Layering). The window's own Esc is spent on the sheet, so this runs first, on the way down.
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        var open = this.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Flyout?.IsOpen == true);
        if (open?.Flyout is not { } flyout)
        {
            return;
        }

        flyout.Hide();
        _ = open.Focus(NavigationMethod.Tab);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        _frame?.FocusRequested -= OnFocusRequested;
        base.OnDataContextChanged(e);
        _frame = DataContext as InstanceFrameViewModel;
        _frame?.FocusRequested += OnFocusRequested;
        if (_frame?.WantedFocus is { } waiting)
        {
            OnFocusRequested(_frame, waiting);   // asked for before this view had its context
        }
    }

    // A key opened this frame wanting focus on one of its parts (A on a desk task: the awaiting output's Copy; X: Keep). The part may not be
    // laid out yet, and the sheet's own focus move may follow, so it is tried after each layout pass and once more after the queue
    // has run, until it holds focus.
    private void OnFocusRequested(object? sender, FrameFocusEventArgs e)
    {
        _wanted = e;
        _attempts = 0;
        LayoutUpdated -= OnLayoutUpdated;
        LayoutUpdated += OnLayoutUpdated;
        Dispatcher.UIThread.Post(Satisfy, DispatcherPriority.Background);
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) => Satisfy();

    private void Satisfy()
    {
        if (_wanted is not { } wanted || !this.IsAttachedToVisualTree())
        {
            // nothing wanted, or the view left its window (a closed one cannot hold focus): stop, so the queue drains
            (_wanted, _attempts) = (null, 0);
            LayoutUpdated -= OnLayoutUpdated;
            return;
        }

        var target = Part(wanted);
        if (target?.IsFocused == true && _attempts++ > 0)
        {
            (_wanted, _attempts) = (null, 0);   // held it through a second look: the sheet's own move did not take it back
            _frame?.FocusServed();
            LayoutUpdated -= OnLayoutUpdated;
            return;
        }

        if (target is not null)
        {
            _ = target.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.Post(Satisfy, DispatcherPriority.Background);
        }
        else if (++_attempts > MaxLooks)
        {
            GiveUp(wanted);
        }
    }

    // The part never came: a child that is not drawn (past "N more") falls back to the frame's first read action, so focus never lands nowhere.
    private void GiveUp(FrameFocusEventArgs wanted)
    {
        LayoutUpdated -= OnLayoutUpdated;
        if (wanted.Target is FrameFocus.ChildEntry or FrameFocus.ChildItem or FrameFocus.ChildPointer or FrameFocus.Children && _frame is { } frame)
        {
            OnFocusRequested(frame, new FrameFocusEventArgs(frame.InitialFocus, key: null));
            return;
        }

        (_wanted, _attempts) = (null, 0);
        _frame?.FocusServed();
    }

    private Button? Part(FrameFocusEventArgs wanted)
    {
        var buttons = this.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.IsEffectivelyEnabled).ToList();
        Button? Find(string name, Func<Button, bool>? also = null) => buttons.Find(b => string.Equals(b.Name, name, StringComparison.Ordinal) && (also?.Invoke(b) ?? true));
        return wanted.Target switch
        {
            FrameFocus.Keep => Find("ExpandedKeep"),
            FrameFocus.OpenIn => Find("InstanceOpenIn"),
            FrameFocus.CopyStatus => Find("ExpandedCopyStatus"),
            FrameFocus.PendingOutput => Find("FactCopyCli", b => b.DataContext is FactRowViewModel row && string.Equals(row.Key, wanted.Key, StringComparison.Ordinal)) ?? Find("ExpandedCopyStatus"),
            FrameFocus.Back => Find("InstanceBack") ?? SheetBack(),   // the 404 state's own Back hides where the sheet covers the page: the header's Back is the one then
            FrameFocus.ChildEntry => Find("ChildRowOpen", b => b.DataContext is ChildRowViewModel child && string.Equals(child.Address, wanted.Key, StringComparison.Ordinal)),
            FrameFocus.ChildItem => Find("ChildRowItemOpen", b => b.DataContext is ChildRowViewModel child && string.Equals(child.ItemId, wanted.Key, StringComparison.Ordinal)),
            FrameFocus.ChildPointer => Find("ExpandedChild"),
            FrameFocus.Recipe => Find("InstanceRecipe"),
            FrameFocus.Children => Find("ChildRowOpen"),
            _ => throw new ArgumentOutOfRangeException(nameof(wanted), wanted.Target, message: null),
        };
    }

    private Button? SheetBack() => TopLevel.GetTopLevel(this)?.GetVisualDescendants().OfType<Button>()
        .FirstOrDefault(b => string.Equals(b.Name, "SheetBack", StringComparison.Ordinal) && b.IsEffectivelyVisible && b.IsEffectivelyEnabled);

    private void OnBack(object? sender, RoutedEventArgs e) => (TopLevel.GetTopLevel(this)?.DataContext as ShellViewModel)?.Back();

    private void OnRecipe(object? sender, RoutedEventArgs e) => _frame?.OpenRecipe();

    private void OnCopyAddress(object? sender, RoutedEventArgs e) => _ = _frame?.CopyAddress(CancellationToken.None);

    private void OnOpenIn(object? sender, RoutedEventArgs e) => _ = (DataContext as InstanceFrameViewModel)?.Subject?.OpenIn(CancellationToken.None);
}
#pragma warning restore MA0182
