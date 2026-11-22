using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;

namespace Aw.Sindri.Ui.Views.Controls;

#pragma warning disable MA0182 // until the Workflows view swaps this part in (#3234)

/// <summary>
/// The labelled field (#3234): a caption, the input and the field's <see cref="FieldError"/> line in one column. An error sentence draws the input's border red (class "field-invalid", styled in ControlStyles.axaml for rest,
/// hover and focus alike) and shows the line; clearing it undoes both. The caption is the input's accessible name, so the screen reader does not read it twice. One part replaces the caption, input and error rows repeated in WorkflowEditorSheet.axaml.
/// </summary>
internal sealed class LabelledField : ContentControl
{
    /// <summary>The caption above the input.</summary>
    public static readonly StyledProperty<string?> CaptionProperty = AvaloniaProperty.Register<LabelledField, string?>(nameof(Caption));

    /// <summary>The error sentence under the input; empty hides the line and the red border.</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty = AvaloniaProperty.Register<LabelledField, string?>(nameof(ErrorText));

    static LabelledField()
    {
        _ = CaptionProperty.Changed.AddClassHandler<LabelledField>((f, _) => f.Sync());
        _ = ErrorTextProperty.Changed.AddClassHandler<LabelledField>((f, _) => f.Sync());
        _ = ContentProperty.Changed.AddClassHandler<LabelledField>((f, _) => f.Sync());
    }

    /// <summary>Initializes a new instance of the <see cref="LabelledField"/> class.</summary>
    public LabelledField()
    {
        Template = new FuncControlTemplate<LabelledField>((field, scope) =>
        {
            var caption = new TextBlock { Name = "PART_Caption" };
            caption.Classes.Add("caption");
            _ = caption.Bind(TextBlock.TextProperty, field.GetBindingObservable(CaptionProperty));
            AutomationProperties.SetAccessibilityView(caption, AccessibilityView.Raw);
            var input = new ContentPresenter { Name = "PART_ContentPresenter", Margin = new Thickness(0, 4, 0, 0) };
            _ = input.Bind(ContentPresenter.ContentProperty, field.GetBindingObservable(ContentProperty));
            var line = new FieldError { Name = "PART_Error", Margin = new Thickness(0, 4, 0, 0) };
            _ = line.Bind(FieldError.TextProperty, field.GetBindingObservable(ErrorTextProperty));
            return new StackPanel { Children = { caption, input, line } };
        });
    }

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

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(LabelledField);

    // The input is named by the caption and wears the red border while there is an error sentence.
    private void Sync()
    {
        if (Content is not Control input)
        {
            return;
        }

        AutomationProperties.SetName(input, Caption);
        input.Classes.Set("field-invalid", !string.IsNullOrEmpty(ErrorText));
    }
}
