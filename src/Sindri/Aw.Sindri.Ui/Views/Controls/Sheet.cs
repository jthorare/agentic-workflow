using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.Views.Icons;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// The sheet (#3234): one layout for a panel that asks something and waits (a picker, a target list, a confirm): a heading, a body, an error
/// line with its code under Details, and a row of buttons. First focus lands inside when it opens (the body's first field or button, else the
/// heading), Tab stays inside it, Esc closes it and hands focus back to what had it. The sheet's one primary button is the caller's:
/// a danger one is a <c>command-control destructive primary</c> button (ErrorFill, white text at AA).
/// </summary>
internal sealed class Sheet : UserControl
{
    /// <summary>The heading.</summary>
    public static readonly StyledProperty<string?> HeadingProperty = AvaloniaProperty.Register<Sheet, string?>(nameof(Heading));

    /// <summary>The body.</summary>
    public static readonly StyledProperty<Control?> BodyProperty = AvaloniaProperty.Register<Sheet, Control?>(nameof(Body));

    /// <summary>The buttons, left to right; the first one that is a primary button is the sheet's one primary.</summary>
    public static readonly StyledProperty<IReadOnlyList<Control>?> ButtonsProperty = AvaloniaProperty.Register<Sheet, IReadOnlyList<Control>?>(nameof(Buttons));

    /// <summary>The error sentence; empty hides the line.</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty = AvaloniaProperty.Register<Sheet, string?>(nameof(ErrorText));

    /// <summary>The error's code, shown under Details.</summary>
    public static readonly StyledProperty<string?> ErrorCodeProperty = AvaloniaProperty.Register<Sheet, string?>(nameof(ErrorCode));

    /// <summary>Whether the sheet is open; opening moves first focus inside.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<Sheet, bool>(nameof(IsOpen));

    private readonly Border _heading = new() { Focusable = true };
    private readonly TextBlock _headingText = new();
    private readonly ContentControl _body = new() { IsTabStop = false };
    private readonly Border _error = new() { IsVisible = false };
    private readonly TextBlock _errorText = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private readonly Expander _details = new() { Header = "Details", IsExpanded = false, IsVisible = false };
    private readonly TextBlock _code = new();
    private readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    private IInputElement? _returnTo;

    static Sheet()
    {
        _ = HeadingProperty.Changed.AddClassHandler<Sheet>((sheet, _) => sheet.ShowHeading());
        _ = BodyProperty.Changed.AddClassHandler<Sheet>((sheet, _) => sheet._body.Content = sheet.Body);
        _ = ButtonsProperty.Changed.AddClassHandler<Sheet>((sheet, _) => sheet.ShowButtons());
        _ = ErrorTextProperty.Changed.AddClassHandler<Sheet>((sheet, _) => sheet.ShowError());
        _ = ErrorCodeProperty.Changed.AddClassHandler<Sheet>((sheet, _) => sheet.ShowError());
        _ = IsOpenProperty.Changed.AddClassHandler<Sheet>((sheet, e) => sheet.Opened(e.GetNewValue<bool>()));
    }

    /// <summary>Initializes a new instance of the <see cref="Sheet"/> class, closed.</summary>
    public Sheet()
    {
        IsVisible = false;

        // The heading is a line of text that takes focus when nothing inside can; it is not a Tab stop (a Tab from the last button wraps to the first field).
        _heading.Classes.Add("sheet-title");
        KeyboardNavigation.SetIsTabStop(_heading, false);
        _headingText.Classes.Add("body-strong");
        _headingText.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        AutomationProperties.SetAccessibilityView(_headingText, AccessibilityView.Raw);
        AutomationProperties.SetAccessibilityView(_heading, AccessibilityView.Control);
        AutomationProperties.SetControlTypeOverride(_heading, Avalonia.Automation.Peers.AutomationControlType.Text);
        _heading.Child = _headingText;

        _error.Classes.Add("callout");
        _error.Classes.Add("fail");
        _error.Margin = new Thickness(0);
        _errorText.Classes.Add("body");
        AutomationProperties.SetAccessibilityView(_errorText, AccessibilityView.Raw);
        var icon = new StateIcon { Glyph = StateGlyph.Down, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 0, 0) };
        icon.Classes.Add("error");
        _code.Classes.Add("caption");
        _code.Classes.Add("mono");
        _details.Content = _code;
        var lines = new StackPanel { Spacing = 4 };
        lines.Children.Add(_errorText);
        lines.Children.Add(_details);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(icon);
        row.Children.Add(lines);
        _error.Child = row;
        AutomationProperties.SetControlTypeOverride(_error, Avalonia.Automation.Peers.AutomationControlType.Text);

        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(_heading);
        stack.Children.Add(_body);
        stack.Children.Add(_error);
        stack.Children.Add(_buttons);
        var panel = new Border { Padding = new Thickness(16), Child = stack };
        panel.Classes.Add("panel");
        Content = panel;
    }

    /// <summary>Raised when Esc closes the sheet.</summary>
    internal event EventHandler? Closed;

    /// <summary>Gets or sets the heading.</summary>
    internal string? Heading
    {
        get => GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    /// <summary>Gets or sets the body.</summary>
    internal Control? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    /// <summary>Gets or sets the buttons.</summary>
    internal IReadOnlyList<Control>? Buttons
    {
        get => GetValue(ButtonsProperty);
        set => SetValue(ButtonsProperty, value);
    }

    /// <summary>Gets or sets the error sentence.</summary>
    internal string? ErrorText
    {
        get => GetValue(ErrorTextProperty);
        set => SetValue(ErrorTextProperty, value);
    }

    /// <summary>Gets or sets the error code.</summary>
    internal string? ErrorCode
    {
        get => GetValue(ErrorCodeProperty);
        set => SetValue(ErrorCodeProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the sheet is open.</summary>
    internal bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Closes the sheet and raises <see cref="Closed"/>.</summary>
    internal void Close()
    {
        IsOpen = false;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsOpen || e.Handled)
        {
            return;
        }

        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            e.Handled = TrapTab(shift: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        }
    }

    private void ShowHeading()
    {
        _headingText.Text = Heading;
        AutomationProperties.SetName(_heading, Heading);
    }

    private void ShowButtons()
    {
        _buttons.Children.Clear();
        _buttons.Children.AddRange(Buttons ?? []);
    }

    private void ShowError()
    {
        var has = !string.IsNullOrEmpty(ErrorText);
        _error.IsVisible = has;
        _errorText.Text = ErrorText;
        AutomationProperties.SetName(_error, ErrorText);
        _details.IsVisible = has && !string.IsNullOrEmpty(ErrorCode);
        _code.Text = ErrorCode;
    }

    private void Opened(bool open)
    {
        IsVisible = open;
        if (open)
        {
            _returnTo = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            Dispatcher.UIThread.Post(FocusFirst, DispatcherPriority.Input);
            return;
        }

        var back = _returnTo;
        _returnTo = null;
        if (back is Visual { IsEffectivelyVisible: true } visual && visual.GetVisualRoot() is not null)
        {
            _ = back.Focus();
        }
    }

    private void FocusFirst()
    {
        if (!IsOpen)
        {
            return;
        }

        var first = Stops().FirstOrDefault(c => !_error.IsVisualAncestorOf(c));
        _ = first is not null ? first.Focus(NavigationMethod.Tab) : _heading.Focus(NavigationMethod.Tab);
    }

    // Tab from the last stop wraps to the first, Shift+Tab from the first wraps to the last: focus never leaves the sheet. Anything else is the ordinary move.
    private bool TrapTab(bool shift)
    {
        var stops = Stops().ToList();
        if (stops.Count == 0)
        {
            return true;
        }

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        var index = stops.FindIndex(c => ReferenceEquals(c, focused));
        if (!shift && index == stops.Count - 1)
        {
            _ = stops[0].Focus(NavigationMethod.Tab);
            return true;
        }

        if (shift && index <= 0)
        {
            _ = stops[^1].Focus(NavigationMethod.Tab);
            return true;
        }

        return false;
    }

    private IEnumerable<Control> Stops() =>
        this.GetVisualDescendants().OfType<Control>().Where(c => c.Focusable && c.IsEffectivelyEnabled && c.IsEffectivelyVisible && KeyboardNavigation.GetIsTabStop(c));
}
