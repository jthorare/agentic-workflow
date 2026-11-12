using Avalonia;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Controls;

#pragma warning disable MA0182 // until the Workflows view swaps this part in (#3234)

/// <summary>The labelled field (#3234): a caption, the input, and the field's error line; an error draws the input's border red. Stub: the red test comes first.</summary>
internal sealed class LabelledField : ContentControl
{
    /// <summary>The caption above the input.</summary>
    public static readonly StyledProperty<string?> CaptionProperty = AvaloniaProperty.Register<LabelledField, string?>(nameof(Caption));

    /// <summary>The error sentence under the input; empty hides the line and the red border.</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty = AvaloniaProperty.Register<LabelledField, string?>(nameof(ErrorText));

    /// <summary>Gets or sets the caption.</summary>
    public string? Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    /// <summary>Gets or sets the error sentence.</summary>
    public string? ErrorText
    {
        get => GetValue(ErrorTextProperty);
        set => SetValue(ErrorTextProperty, value);
    }
}
