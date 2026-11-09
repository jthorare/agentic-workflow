using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One config field of the selected node's form: its name, its type in words, the text being edited, and the line under it when the text can't be sent.</summary>
internal sealed class ConfigFieldViewModel : ObservableObject
{
    internal ConfigFieldViewModel(WorkflowBlockField field, string text)
    {
        Name = field.Name;
        Type = field.Type;
        IsRequired = field.Required;
        (Text, Original) = (text, text);
    }

    /// <summary>The config key.</summary>
    public string Name { get; }

    /// <summary>"text", "boolean", "number", "text list" or "object", as the block list serves it.</summary>
    public string Type { get; }

    /// <summary>True when the config must have the field.</summary>
    public bool IsRequired { get; }

    /// <summary>The text being edited: a text list has one item per line, an object is JSON.</summary>
    public string Text { get; set { Set(ref field, value); OnChanged(nameof(IsOn)); OnChanged(nameof(IsChanged)); } }

    /// <summary>The text the field had when the form was made: Save sends only the fields that differ from it.</summary>
    public string Original { get; }

    /// <summary>True when the text was changed since the form was made.</summary>
    public bool IsChanged => !string.Equals(Text, Original, StringComparison.Ordinal);

    /// <summary>The field's name, with "(required)" when it must have a value.</summary>
    public string Caption => Labels.WfFieldCaption(Name, IsRequired);

    /// <summary>True for a field edited with a check box.</summary>
    public bool IsBoolean => string.Equals(Type, "boolean", StringComparison.Ordinal);

    /// <summary>True for a field edited with several lines (a text list or an object).</summary>
    public bool IsMultiline => string.Equals(Type, "text list", StringComparison.Ordinal) || string.Equals(Type, "object", StringComparison.Ordinal);

    /// <summary>True for a field edited with one line (text and number).</summary>
    public bool IsSingleLine => !IsBoolean && !IsMultiline;

    /// <summary>A boolean field as a check box: checked when the text is "true".</summary>
    public bool IsOn { get => string.Equals(Text.Trim(), "true", StringComparison.OrdinalIgnoreCase); set => Text = value ? "true" : "false"; }

    /// <summary>Why the text can't be sent, or empty.</summary>
    public string ErrorText { get; internal set { Set(ref field, value); OnChanged(nameof(HasError)); } } = string.Empty;

    /// <summary>True when there is a line under the field.</summary>
    public bool HasError => ErrorText.Length > 0;
}
