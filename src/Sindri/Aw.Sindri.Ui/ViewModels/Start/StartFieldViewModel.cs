using System.Text;
using System.Text.Json;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Start;

/// <summary>One input of the chosen recipe as the form asks for it: a box, a link, JSON, or a file.</summary>
internal sealed class StartFieldViewModel : ObservableObject
{
    /// <summary>The most bytes a fact may hold (API: 1 MiB).</summary>
    internal const int MaxBytes = 1024 * 1024;

    private readonly IFilePicker _files;
    private PickedFile? _file;

    /// <summary>The field for one declared input.</summary>
    /// <param name="input">The input as the recipe serves it.</param>
    /// <param name="files">Where a file is chosen (read when a file is asked for).</param>
    internal StartFieldViewModel(RecipeInputView input, IFilePicker files)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(files);
        _files = files;
        Key = input.Key;
        Label = string.IsNullOrWhiteSpace(input.Label) ? Workflows.WorkflowNames.InputName(input.Key) : input.Label;
        Kind = input.Type switch
        {
            FactType.Text => StartFieldKind.Text,
            FactType.File => StartFieldKind.File,
            FactType.Link => StartFieldKind.Link,
            FactType.Item or FactType.ScopeMap or FactType.Packet or FactType.Evidence or FactType.Decision or FactType.Payload or FactType.Findings => StartFieldKind.Payload,
            _ => StartFieldKind.Payload,   // a type this build has no field for is JSON text, like the API reads it
        };
        var generated = input.Generate is not null;
        IsRequired = input.Required && !generated;
        Hint = generated ? Labels.GeneratedIfEmpty : string.Empty;
        Caption = IsRequired ? Label + " (required)" : Label;
        FileLine = Kind == StartFieldKind.File ? Labels.NoFileChosen : string.Empty;
    }

    /// <summary>Raised when the human changes the field (typed, or a file chosen), so the form can drop the sentence about it.</summary>
    internal event EventHandler? Edited;

    /// <summary>The file button's words.</summary>
    public string ChooseFileText { get; } = Labels.ChooseFile;

    /// <summary>The input's key ('fact:spec').</summary>
    public string Key { get; }

    /// <summary>The label shown: the recipe's, else the key.</summary>
    public string Label { get; }

    /// <summary>The words shown above the field, which name it for assistive technology too: the label, and "(required)" when the form cannot send without it.</summary>
    public string Caption { get; }

    /// <summary>How it is asked for.</summary>
    public StartFieldKind Kind { get; }

    /// <summary>True for a text box.</summary>
    public bool IsText => Kind == StartFieldKind.Text;

    /// <summary>True for a link box.</summary>
    public bool IsLink => Kind == StartFieldKind.Link;

    /// <summary>True for JSON text.</summary>
    public bool IsPayload => Kind == StartFieldKind.Payload;

    /// <summary>True for a file.</summary>
    public bool IsFile => Kind == StartFieldKind.File;

    /// <summary>True for a one-line box (text or link).</summary>
    public bool IsLine => Kind is StartFieldKind.Text or StartFieldKind.Link;

    /// <summary>True for a field typed into (text, link, JSON), as against a file.</summary>
    public bool IsTyped => Kind != StartFieldKind.File;

    /// <summary>True when the form cannot send without it: the recipe requires it and does not generate it.</summary>
    public bool IsRequired { get; }

    /// <summary>"generated if empty" for an input the recipe generates; else empty.</summary>
    public string Hint { get; }

    /// <summary>True when <see cref="Hint"/> shows.</summary>
    public bool HasHint => Hint.Length > 0;

    /// <summary>The typed text.</summary>
    public string Value
    {
        get;
        set
        {
            Set(ref field, value);
            ClearError();
        }
    } = string.Empty;

    /// <summary>The chosen file in one line, or "No file chosen".</summary>
    public string FileLine
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(FileButtonName));
        }
    }

    /// <summary>The file button's accessible name: what it does, which input it is for and what is chosen now.</summary>
    public string FileButtonName => Labels.StartFileButtonName(Caption, FileLine);

    /// <summary>The sentence under the field when it cannot be sent, or empty.</summary>
    public string Error
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasError));
            OnChanged(nameof(HelpText));
        }
    } = string.Empty;

    /// <summary>The input's help text for assistive technology: its error while it has one, else the generated-if-empty line.</summary>
    public string HelpText => Error.Length > 0 ? Error : Hint;

    /// <summary>True while <see cref="Error"/> shows.</summary>
    public bool HasError => Error.Length > 0;

    /// <summary>Asks for a file and keeps it.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the choice is shown.</returns>
    internal async Task Pick(CancellationToken cancellationToken)
    {
        var picked = await _files.Pick(Labels.ChooseFileTitle, MaxBytes, cancellationToken).ConfigureAwait(true);
        if (picked is null)
        {
            return;   // none chosen: the file before it, if any, stays
        }

        _file = picked;
        FileLine = Labels.StartFileLine(picked.Name, picked.Size);
        ClearError();
    }

    /// <summary>What the input gives a start.</summary>
    /// <returns>The fact, none, or why not; the reason is also shown under the field.</returns>
    internal FieldCheck Check()
    {
        var checkedField = Kind == StartFieldKind.File ? CheckFile() : CheckTyped();
        Error = checkedField.Problem;
        return checkedField;
    }

    private FieldCheck CheckFile()
    {
        if (_file is null)
        {
            return Missing();
        }

        return _file.Size > MaxBytes
            ? Refused(Labels.StartTooLarge(Label))
            : new FieldCheck(new StartFact(_file.Name, _file.Content), Problem: string.Empty);
    }

    private FieldCheck CheckTyped()
    {
        if (string.IsNullOrWhiteSpace(Value))
        {
            return Missing();
        }

        var bytes = Encoding.UTF8.GetBytes(Kind == StartFieldKind.Link ? Value.Trim() : Value);   // a link is checked trimmed, so it is sent trimmed
        if (bytes.Length > MaxBytes)
        {
            return Refused(Labels.StartTooLarge(Label));
        }

        if (Kind == StartFieldKind.Link && !IsLinkText(Value))
        {
            return Refused(Labels.StartNotLink(Label));
        }

        if (Kind == StartFieldKind.Payload && !IsJson(bytes))
        {
            return Refused(Labels.StartNotJson(Label));
        }

        return new FieldCheck(new StartFact(ShortName + Extension, bytes), Problem: string.Empty);
    }

    // Left empty: a generated or optional input is simply not sent (the recipe renders it); a required one is the human's to give.
    private FieldCheck Missing() => IsRequired ? Refused(Labels.StartRequired(Label)) : new FieldCheck(Fact: null, Problem: string.Empty);

    private static FieldCheck Refused(string problem) => new(Fact: null, problem);

    private string Extension => Kind switch
    {
        StartFieldKind.Link => ".url",
        StartFieldKind.Payload => ".json",
        StartFieldKind.Text or StartFieldKind.File => ".md",
        _ => ".md",
    };

    private string ShortName => Key.StartsWith("fact:", StringComparison.Ordinal) ? Key["fact:".Length..] : Key;

    private static bool IsLinkText(string text) =>
        Uri.TryCreate(text.Trim(), UriKind.Absolute, out var link) && (string.Equals(link.Scheme, "http", StringComparison.Ordinal) || string.Equals(link.Scheme, "https", StringComparison.Ordinal));

    private static bool IsJson(byte[] bytes)
    {
        try
        {
            using var json = JsonDocument.Parse(bytes);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void ClearError()
    {
        if (Error.Length > 0)
        {
            Error = string.Empty;
        }

        Edited?.Invoke(this, EventArgs.Empty);
    }
}
