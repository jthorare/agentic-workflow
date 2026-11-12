using Avalonia;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// The sheet (#3234): one layout for a panel that asks something and waits (a picker, a target list, a confirm): a heading, a body, an error
/// line with its code under Details, and a row of buttons. First focus lands inside when it opens, Tab stays inside it, Esc closes it.
/// Stub: the layout and the behaviour come with the green commit.
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
}
