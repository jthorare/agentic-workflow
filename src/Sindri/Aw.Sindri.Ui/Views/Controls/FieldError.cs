using Avalonia;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Controls;

#pragma warning disable MA0182 // until the Workflows view swaps this part in (#3234)

/// <summary>The field error line (#3234): a failed field's sentence in the Error colour, after a ✕. Stub: the red test comes first.</summary>
internal sealed class FieldError : Border
{
    /// <summary>The error sentence; empty hides the line.</summary>
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<FieldError, string?>(nameof(Text));

    /// <summary>Gets or sets the error sentence.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
