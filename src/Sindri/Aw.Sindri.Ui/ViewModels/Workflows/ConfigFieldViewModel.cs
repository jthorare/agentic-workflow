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
        Text = text;
    }

    /// <summary>The config key.</summary>
    public string Name { get; }

    /// <summary>"text", "boolean", "number", "text list" or "object", as the block list serves it.</summary>
    public string Type { get; }

    /// <summary>True when the config must have the field.</summary>
    public bool IsRequired { get; }

    /// <summary>The text being edited: a text list has one item per line, an object is JSON.</summary>
    public string Text { get; internal set => Set(ref field, value); }

    /// <summary>Why the text can't be sent, or empty.</summary>
    public string ErrorText { get; internal set { Set(ref field, value); OnChanged(nameof(HasError)); } } = string.Empty;

    /// <summary>True when there is a line under the field.</summary>
    public bool HasError => ErrorText.Length > 0;
}
