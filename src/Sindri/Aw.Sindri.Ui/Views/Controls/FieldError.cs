using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.Views.Icons;

namespace Aw.Sindri.Ui.Views.Controls;

#pragma warning disable MA0182 // until the Workflows view swaps this part in (#3234)

/// <summary>
/// The field error line (#3234): a failed field's sentence in the Error colour (4.5:1 on the panel) after a ✕, so colour is never the only signal. An empty sentence hides the whole line. The screen reader
/// reads it as text with the sentence as its name. One part replaces the Workflows editor's unstyled EditorFieldError, EditorFailure and EditFailureName.
/// </summary>
internal sealed class FieldError : Border
{
    /// <summary>The error sentence; empty hides the line.</summary>
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<FieldError, string?>(nameof(Text));

    private readonly TextBlock _words = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };

    static FieldError()
    {
        _ = TextProperty.Changed.AddClassHandler<FieldError>((line, _) => line.Refresh());
    }

    /// <summary>Initializes a new instance of the <see cref="FieldError"/> class.</summary>
    public FieldError()
    {
        _words.Classes.Add("error");
        _words.Classes.Add("caption");
        var cross = new StateIcon { Glyph = StateGlyph.Down, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 8, 0) };
        cross.Classes.Add("error");
        Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { cross, _words } };
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Text);
        Refresh();
    }

    /// <summary>Gets or sets the error sentence.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private void Refresh()
    {
        _words.Text = Text;
        IsVisible = !string.IsNullOrEmpty(Text);
        AutomationProperties.SetName(this, Text);
    }
}
