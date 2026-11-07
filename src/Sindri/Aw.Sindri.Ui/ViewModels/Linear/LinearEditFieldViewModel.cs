using System.Text.Json.Nodes;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>
/// One editable field of the Linear issue sheet: what the sheet opened with (the baseline), what the owner has now (the draft), and the choices a picker offers.
/// Both are JSON values under the change route's own field names, so a changed field goes into the change body as it is.
/// </summary>
internal sealed class LinearEditFieldViewModel : ObservableObject
{
    private string _fixedText = string.Empty;
    private bool _available = true;
    private bool _locked;

    /// <summary>Makes a field.</summary>
    /// <param name="api">The change body's name for it (stateId, priority, labelIds, ...).</param>
    /// <param name="label">Its label in the grid.</param>
    /// <param name="kind">How it is edited.</param>
    internal LinearEditFieldViewModel(string api, string label, LinearEditKind kind)
    {
        (Api, Label, Kind) = (api, label, kind);
        Baseline = kind == LinearEditKind.Text ? JsonValue.Create(string.Empty) : null;
        Current = Baseline?.DeepClone();
    }

    /// <summary>The edited fields changed their draft.</summary>
    internal event EventHandler? Edited;

    /// <summary>The field's name in the change body.</summary>
    public string Api { get; }

    /// <summary>The field's label.</summary>
    public string Label { get; }

    /// <summary>How the field is edited.</summary>
    public LinearEditKind Kind { get; }

    /// <summary>True for a text box.</summary>
    public bool IsText => Kind == LinearEditKind.Text;

    /// <summary>True for a picker of one choice.</summary>
    public bool IsChoice => Kind == LinearEditKind.Choice;

    /// <summary>True for ticks.</summary>
    public bool IsTicks => Kind == LinearEditKind.Ticks;

    /// <summary>True for a field the owner can change; false for the project, which is only shown.</summary>
    public bool IsEditable => Kind != LinearEditKind.Fixed;

    /// <summary>True while the control takes input: its choices exist and no save is running.</summary>
    public bool IsEnabled => _available && !_locked;

    /// <summary>Why a picker has no choices ("ENG's states haven't synced yet"); empty when it has.</summary>
    public string UnavailableText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when <see cref="UnavailableText"/> is shown.</summary>
    public bool HasUnavailableText => UnavailableText.Length > 0;

    /// <summary>True when the draft differs from the baseline.</summary>
    public bool IsChanged => IsEditable && !Same(Baseline, Current);

    /// <summary>The value in words.</summary>
    public string Value => Kind == LinearEditKind.Fixed ? _fixedText : Describe(Current);

    /// <summary>The field's accessible name: "State: Done", with ", changed" once the owner changed it.</summary>
    public string Name => IsChanged ? Labels.LinearChangedName(Label + ": " + Value) : Label + ": " + Value;

    /// <summary>The text of a text field.</summary>
    public string Text
    {
        get => Current is JsonValue v && v.TryGetValue<string>(out var text) ? text : string.Empty;
        set => Take(JsonValue.Create(value));
    }

    /// <summary>The picker's entries, or the labels.</summary>
    public IReadOnlyList<LinearChoiceViewModel> Options { get; private set; } = [];

    /// <summary>The picker's chosen entry; setting null changes nothing (a picker with no entries reports null).</summary>
    public LinearChoiceViewModel? Selected
    {
        get => IsChoice ? Options.FirstOrDefault(o => JsonNode.DeepEquals(o.Value, Current)) : null;
        set
        {
            if (value is not null && IsChoice)
            {
                Take(value.Value?.DeepClone());
            }
        }
    }

    /// <summary>What the sheet opened with.</summary>
    internal JsonNode? Baseline { get; private set; }

    /// <summary>What the owner has now.</summary>
    internal JsonNode? Current { get; private set; }

    /// <summary>Sets the value the sheet opened with, and the draft with it unless the owner changed the field and <paramref name="keepEdit"/> asks to keep that.</summary>
    /// <param name="baseline">The value as Linear has it.</param>
    /// <param name="keepEdit">True to keep a changed draft.</param>
    internal void Load(JsonNode? baseline, bool keepEdit)
    {
        var edited = keepEdit && IsChanged;
        Baseline = baseline?.DeepClone();
        if (!edited)
        {
            Current = Baseline?.DeepClone();
        }

        Refresh();
    }

    /// <summary>Takes a value as both baseline and draft (the owner took Linear's).</summary>
    /// <param name="value">The value.</param>
    internal void Rebase(JsonNode? value)
    {
        (Baseline, Current) = (value?.DeepClone(), value?.DeepClone());
        Refresh();
        Edited?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Sets only the baseline (Linear's value the 409 just showed), keeping the draft.</summary>
    /// <param name="baseline">The value.</param>
    internal void Rebaseline(JsonNode? baseline)
    {
        Baseline = baseline?.DeepClone();
        Refresh();
    }

    /// <summary>Drops the owner's edit.</summary>
    internal void Revert()
    {
        Current = Baseline?.DeepClone();
        Refresh();
        Edited?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Shows a value in words that is never edited (the project).</summary>
    /// <param name="text">The text.</param>
    internal void ShowFixed(string text)
    {
        _fixedText = text;
        OnChanged(nameof(Value));
        OnChanged(nameof(Name));
    }

    /// <summary>Replaces the entries (a picker's choices, the labels) and re-reads which is chosen.</summary>
    /// <param name="options">The entries.</param>
    internal void SetOptions(IReadOnlyList<LinearChoiceViewModel> options)
    {
        foreach (var old in Options)
        {
            old.Ticked -= OnTicked;
        }

        Options = options;
        foreach (var option in Options)
        {
            option.Ticked += OnTicked;
        }

        Refresh();
    }

    /// <summary>Says whether the control takes input, and why not.</summary>
    /// <param name="available">False when the entries cannot be offered.</param>
    /// <param name="unavailableText">The reason.</param>
    internal void SetAvailable(bool available, string unavailableText)
    {
        _available = available;
        UnavailableText = unavailableText;
        OnChanged(nameof(IsEnabled));
        OnChanged(nameof(HasUnavailableText));
    }

    /// <summary>Locks the control while a save is out (D6's compared values must not change under it).</summary>
    /// <param name="locked">True to lock.</param>
    internal void SetLocked(bool locked)
    {
        _locked = locked;
        OnChanged(nameof(IsEnabled));
    }

    /// <summary>Describes a value in words with this field's entries.</summary>
    /// <param name="value">A value of this field.</param>
    /// <returns>The words.</returns>
    internal string Describe(JsonNode? value) => Kind switch
    {
        LinearEditKind.Text => value is JsonValue v && v.TryGetValue<string>(out var text) ? text : string.Empty,
        LinearEditKind.Ticks => DescribeTicks(value),
        LinearEditKind.Choice => Options.FirstOrDefault(o => JsonNode.DeepEquals(o.Value, value))?.Name ?? value?.ToString() ?? Labels.LinearFieldNone,
        LinearEditKind.Fixed => _fixedText,
        _ => string.Empty,
    };

    private string DescribeTicks(JsonNode? value)
    {
        var ids = IdsOf(value);
        var names = Options.Where(o => o.Value is JsonValue id && id.TryGetValue<string>(out var s) && ids.Contains(s)).Select(o => o.Name).ToList();
        return names.Count > 0 ? string.Join(", ", names) : Labels.LinearFieldNone;
    }

    private void Take(JsonNode? value)
    {
        if (Same(Current, value))
        {
            return;
        }

        Current = value;
        Refresh();
        Edited?.Invoke(this, EventArgs.Empty);
    }

    private void OnTicked(object? sender, EventArgs e)
    {
        Current = new JsonArray([.. Options.Where(o => o.IsTicked).Select(o => o.Value?.DeepClone())]);
        Refresh();
        Edited?.Invoke(this, EventArgs.Empty);
    }

    private void Refresh()
    {
        if (IsTicks)
        {
            var ids = IdsOf(Current);
            foreach (var option in Options)
            {
                option.Reset(option.Value is JsonValue id && id.TryGetValue<string>(out var s) && ids.Contains(s));
            }
        }

        OnChanged(nameof(Options));
        OnChanged(nameof(Selected));
        OnChanged(nameof(Text));
        OnChanged(nameof(Value));
        OnChanged(nameof(IsChanged));
        OnChanged(nameof(Name));
    }

    private static HashSet<string> IdsOf(JsonNode? value) =>
        value is JsonArray array ? [.. array.Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty)] : [];

    // Ticks compare as sets (order is not a change); every other value compares by content.
    private static bool Same(JsonNode? a, JsonNode? b) =>
        a is JsonArray && b is JsonArray ? IdsOf(a).SetEquals(IdsOf(b)) : JsonNode.DeepEquals(a, b);
}
