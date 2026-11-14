using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One config field of the selected node's form: its name, its type in words, the text being edited, and the line under it when the text can't be sent.</summary>
internal sealed class ConfigFieldViewModel : ObservableObject
{
    private static readonly string[] CodeAdvanced = ["repo", "branch", "timeout", "memoryMb"];

    internal ConfigFieldViewModel(WorkflowBlockField field, string text, string blockType = "", string example = "", IReadOnlyList<string>? connections = null)
    {
        ConnectService = ServiceOf(blockType, field.Name);
        ConnectionChoices = ConnectService.Length == 0 ? [] : [.. (connections ?? []).Where(n => n.StartsWith(ConnectService + ":", StringComparison.Ordinal))];
        var isCodeBlock = string.Equals(blockType, "code", StringComparison.Ordinal);
        IsCode = isCodeBlock && string.Equals(field.Name, "code", StringComparison.Ordinal);
        InAdvanced = isCodeBlock && CodeAdvanced.Contains(field.Name, StringComparer.Ordinal);
        Example = example;
        Name = field.Name;
        Type = field.Type;
        IsRequired = field.Required;
        Caption = Labels.WfFieldCaption(field.Caption.Length > 0 ? field.Caption : field.Name, field.Required);
        IsClosedList = IsConnection || field.Choices is { Count: > 0 };
        IReadOnlyList<WorkflowBlockChoice>? listed = IsConnection ? ConnectionChoices.Select(n => new WorkflowBlockChoice(n, n)).ToList() : IsClosedList ? field.Choices : field.Presets;
        Presets = listed ?? [];
        HidesRawText = !IsClosedList && Presets.Any(p => !string.Equals(p.Label, p.Value, StringComparison.Ordinal));
        (Text, Original) = (text, text);
        ShowAdvanced = text.Length > 0 && SelectedPreset is null;   // a value no preset names must be seen, not hidden
    }

    /// <summary>The values to pick from: the closed list when the field has one, else its presets; empty when it has neither.</summary>
    public IReadOnlyList<WorkflowBlockChoice> Presets { get; }

    /// <summary>True when the field is picked from a list (a closed list or presets) and its type is one line.</summary>
    public bool HasPresets => Presets.Count > 0 && IsSingleLine;

    /// <summary>True when only the listed values are valid: no text box.</summary>
    public bool IsClosedList { get; }

    /// <summary>True when the presets' labels differ from their values ("Every weekday at 7:00" for "0 7 * * 1-5"): the raw text is an advanced view. A time zone's labels are its values, so its text stays beside the list.</summary>
    public bool HidesRawText { get; }

    /// <summary>True when the Advanced toggle is shown.</summary>
    public bool HasAdvanced => HasPresets && HidesRawText;

    /// <summary>The Advanced toggle's words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string AdvancedText => Labels.WfFieldAdvanced;
#pragma warning restore CA1822, MA0041

    /// <summary>True when the raw text is open.</summary>
    public bool ShowAdvanced { get; set { Set(ref field, value); OnChanged(nameof(IsRawVisible)); } }

    /// <summary>True when the text box shows: a one-line field with no closed list, and no presets hiding its raw text unless Advanced is open.</summary>
    public bool IsRawVisible => IsSingleLine && !IsClosedList && (!HidesRawText || ShowAdvanced);

    /// <summary>The preset the text equals, or null; picking one sets the text.</summary>
    public WorkflowBlockChoice? SelectedPreset
    {
        get => Presets.FirstOrDefault(p => string.Equals(p.Value, Text.Trim(), StringComparison.Ordinal));
        set
        {
            if (value is not null)
            {
                Text = value.Value;
            }
        }
    }

    /// <summary>True for a connection step's connection field: a picker, never free text.</summary>
    public bool IsConnection => ConnectService.Length > 0;

    /// <summary>The service the step needs ("github"); empty on any other field.</summary>
    public string ConnectService { get; }

    // A step names its service by block type and field name for now: connection_event watches GitHub's events.
    private static string ServiceOf(string blockType, string field) =>
        string.Equals(blockType, "connection_event", StringComparison.Ordinal) && string.Equals(field, "connection", StringComparison.Ordinal) ? "github" : string.Empty;

    /// <summary>The connected connections of the service the step needs.</summary>
    public IReadOnlyList<string> ConnectionChoices { get; }

    /// <summary>True when no connection fits: the field offers Connect instead.</summary>
    public bool NeedsConnect => IsConnection && ConnectionChoices.Count == 0;

    /// <summary>The Connect button's words ("Connect GitHub").</summary>
    public string ConnectText => IsConnection ? Labels.WfConnectService(ConnectService) : string.Empty;

    /// <summary>True for the Code step's program: a monospace multi-line box.</summary>
    public bool IsCode { get; }

    /// <summary>The block's example for this field, shown under it; empty when it has none.</summary>
    public string Example { get; }

    /// <summary>True when there is an example under the field.</summary>
    public bool HasExample => Example.Length > 0;

    /// <summary>True when the field sits in the closed Advanced group.</summary>
    public bool InAdvanced { get; }

    /// <summary>True when the field is drawn: it is not in the Advanced group, or the group is open.</summary>
    public bool IsShown => !InAdvanced || GroupOpen;

    /// <summary>True when the Advanced group this field may sit in is open; the editor sets it.</summary>
    public bool GroupOpen { get; internal set { Set(ref field, value); OnChanged(nameof(IsShown)); OnChanged(nameof(IsRawVisible)); } }

    /// <summary>The config key.</summary>
    public string Name { get; }

    /// <summary>"text", "boolean", "number", "text list" or "object", as the block list serves it.</summary>
    public string Type { get; }

    /// <summary>True when the config must have the field.</summary>
    public bool IsRequired { get; }

    /// <summary>The text being edited: a text list has one item per line, an object is JSON.</summary>
    public string Text { get; set { Set(ref field, value); OnChanged(nameof(IsOn)); OnChanged(nameof(IsChanged)); OnChanged(nameof(SelectedPreset)); } }

    /// <summary>The text the field had when the form was made: Save sends only the fields that differ from it.</summary>
    public string Original { get; }

    /// <summary>True when the text was changed since the form was made.</summary>
    public bool IsChanged => !string.Equals(Text, Original, StringComparison.Ordinal);

    /// <summary>The field's caption from the API (its name when none came), with "(required)" when it must have a value.</summary>
    public string Caption { get; }

    /// <summary>True for a field edited with a check box.</summary>
    public bool IsBoolean => string.Equals(Type, "boolean", StringComparison.Ordinal);

    /// <summary>True for a field edited with several lines (a text list or an object).</summary>
    public bool IsMultiline => IsCode || string.Equals(Type, "text list", StringComparison.Ordinal) || string.Equals(Type, "object", StringComparison.Ordinal);

    /// <summary>True for a multi-line field that is not the code box: it keeps the small proportional box.</summary>
    public bool IsPlainLines => IsMultiline && !IsCode;

    /// <summary>True for a field edited with one line (text and number).</summary>
    public bool IsSingleLine => !IsBoolean && !IsMultiline;

    /// <summary>A boolean field as a check box: checked when the text is "true". Unchecking writes "false", so an unset field checked and then unchecked sends false rather than removing the key: the box shows a value, and the value it shows is the one sent.</summary>
    public bool IsOn { get => string.Equals(Text.Trim(), "true", StringComparison.OrdinalIgnoreCase); set => Text = value ? "true" : "false"; }

    /// <summary>Why the text can't be sent, or empty.</summary>
    public string ErrorText { get; internal set { Set(ref field, value); OnChanged(nameof(HasError)); } } = string.Empty;

    /// <summary>True when there is a line under the field.</summary>
    public bool HasError => ErrorText.Length > 0;
}
