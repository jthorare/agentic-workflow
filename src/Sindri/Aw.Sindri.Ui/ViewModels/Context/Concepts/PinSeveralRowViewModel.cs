using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>
/// One row of the Add several sheet (concept pins spec §3.6): a concept of the pasted list with its split, its match, its words and features, and the one line that says why it is or is not ticked.
/// The name and definition are editable, so a wrong split is fixed here; the sheet re-checks every row when one changes.
/// </summary>
#pragma warning disable MA0182 // Q7b: the Add several view model builds them
internal sealed class PinSeveralRowViewModel : ObservableObject
{
    private readonly Action _edited;
    private string _name;
    private string _definition;
    private bool _ticked;
    private int? _duplicateOf;
    private PinSeveralChoice _choice = PinSeveralChoice.New;

    internal PinSeveralRowViewModel(PinListLine line, Action edited)
    {
        ArgumentNullException.ThrowIfNull(line);
        (Number, Text, AlsoWords, _edited) = (line.Number, line.Text, line.Words, edited);
        (_name, _definition) = (line.Name, line.Definition ?? string.Empty);
        _ticked = line.Readable;
        Choices = [PinSeveralChoice.New];
    }

    /// <summary>Gets a value indicating whether the person changed the row's name, definition or tick.</summary>
    internal bool Edited { get; private set; }

    /// <summary>The row's 1-based place in the list.</summary>
    public int Number { get; }

    /// <summary>The line or block as pasted.</summary>
    public string Text { get; }

    /// <summary>The words of the line's "(also: …)".</summary>
    internal IReadOnlyList<string> AlsoWords { get; }

    /// <summary>The name; editing it re-checks every row.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (string.Equals(_name, value, StringComparison.Ordinal))
            {
                return;
            }

            _name = value ?? string.Empty;
            Edited = true;
            OnChanged(nameof(Name));
            OnChanged(nameof(SplitText));
            _edited();
        }
    }

    /// <summary>The definition, empty when the line gives none.</summary>
    public string Definition
    {
        get => _definition;
        set
        {
            if (string.Equals(_definition, value, StringComparison.Ordinal))
            {
                return;
            }

            _definition = value ?? string.Empty;
            Edited = true;
            OnChanged(nameof(Definition));
            OnChanged(nameof(SplitText));
        }
    }

    /// <summary>Gets a value indicating whether the name has 1 to 80 characters.</summary>
    internal bool Readable => _name.Trim().Length is > 0 and <= PinListParser.MaxName;

    /// <summary>The row's place as the row says it ("line 3").</summary>
    public string LineText => Labels.PinSeveralLine(Number);

    /// <summary>How the row split its line ("Name: Payments · Definition: How we take money…").</summary>
    public string SplitText => Labels.PinSeveralSplit(_name.Trim(), _definition.Trim());

    /// <summary>Whether the row goes into the write.</summary>
    public bool Ticked
    {
        get => _ticked;
        set
        {
            if (value && !CanTick)
            {
                return;
            }

            if (_ticked != value)
            {
                _ticked = value;
                Edited = true;
                OnChanged(nameof(Ticked));
                OnChanged(nameof(IsWritten));
                _edited();
            }
        }
    }

    /// <summary>Gets a value indicating whether the row can be ticked: it has a name and is the first line of that name.</summary>
    public bool CanTick => Readable && _duplicateOf is null;

    /// <summary>Gets a value indicating whether the row will be written.</summary>
    internal bool IsWritten => _ticked && CanTick;

    /// <summary>The pin this row updates, or null when its name is a new one.</summary>
    internal PinView? Updates { get; private set; }

    /// <summary>The line under the split: why the row isn't ticked, or what ticking does.</summary>
    public string Note { get; private set; } = string.Empty;

    /// <summary>Whether the note has words.</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>The line about the match: "Matching your list…", the failure, or "Also matched to Refunds; pick another or New".</summary>
    public string MatchNote { get; private set; } = string.Empty;

    /// <summary>Whether the match note has words.</summary>
    public bool HasMatchNote => MatchNote.Length > 0;

    /// <summary>The line about the words when the concept's own couldn't be read; empty otherwise.</summary>
    public string WordsNote { get; private set; } = string.Empty;

    /// <summary>Whether the words note has words.</summary>
    public bool HasWordsNote => WordsNote.Length > 0;

    /// <summary>The picker's choices: "New, no match yet" first, then the map's derived concepts.</summary>
    public IReadOnlyList<PinSeveralChoice> Choices { get; private set; }

    /// <summary>The match the picker shows.</summary>
    public PinSeveralChoice SelectedChoice
    {
        get => _choice;
        set
        {
            if (value is not null && !ReferenceEquals(_choice, value))
            {
                _choice = value;
                OnChanged(nameof(SelectedChoice));
                OnChanged(nameof(AccessibleName));
                _edited();
            }
        }
    }

    /// <summary>The concept key the words and features were filled from; null for none.</summary>
    internal string? FilledFrom { get; set; }

    /// <summary>The words people use, from the line's "(also: …)" and the matched concept's terms.</summary>
    public IReadOnlyList<PinWordRow> Words
    {
        get;
        private set
        {
            field = value;
            OnChanged(nameof(Words));
        }
    } = [];

    /// <summary>The matched concept's member features, all ticked.</summary>
    internal IReadOnlyList<PinFeatureRow> Features
    {
        get;
        private set
        {
            field = value;
            OnChanged(nameof(Features));
            OnChanged(nameof(FeaturesText));
            OnChanged(nameof(HasFeatures));
        }
    } = [];

    /// <summary>"Features: Checkout, Refunds"; empty when there are none.</summary>
    public string FeaturesText => Features.Count == 0 ? string.Empty : Labels.PinSeveralFeatures(Features.Select(f => f.Label));

    /// <summary>Whether the row shows features.</summary>
    public bool HasFeatures => Features.Count > 0;

    /// <summary>Whether the row's match is being read.</summary>
    public bool IsMatching { get; private set; }

    /// <summary>The row as a screen reader reads it: "line 2, Payments, matched to Billing".</summary>
    public string AccessibleName => Labels.PinSeveralRowName(Number, _name.Trim(), _choice.Key is null ? null : _choice.Name);

    /// <summary>Gives the row its picker's choices and the match the suggest read made.</summary>
    internal void Choose(IReadOnlyList<PinSeveralChoice> choices, PinSeveralChoice choice)
    {
        Choices = choices;
        OnChanged(nameof(Choices));
        _choice = choice;
        OnChanged(nameof(SelectedChoice));
        OnChanged(nameof(AccessibleName));
    }

    /// <summary>Sets the row's words and features, and the note when the concept's words couldn't be read.</summary>
    internal void Fill(IReadOnlyList<PinWordRow> words, IReadOnlyList<PinFeatureRow> features, string wordsNote)
    {
        Words = words;
        Features = features;
        WordsNote = wordsNote;
        OnChanged(nameof(WordsNote));
        OnChanged(nameof(HasWordsNote));
    }

    /// <summary>Sets whether the match is being read, and the line about the match.</summary>
    internal void Matching(bool matching, string note)
    {
        IsMatching = matching;
        MatchNote = note;
        OnChanged(nameof(IsMatching));
        OnChanged(nameof(MatchNote));
        OnChanged(nameof(HasMatchNote));
    }

    /// <summary>Sets what the sheet found out about the row's name: a pin it updates, or the line it repeats.</summary>
    /// <param name="updates">The pin whose name or id this is, or null.</param>
    /// <param name="duplicateOf">The line number of the earlier row with this name, or null.</param>
    /// <returns>True when the pin the row updates changed, so its words need filling again.</returns>
    internal bool Judge(PinView? updates, int? duplicateOf)
    {
        var changed = !string.Equals(Updates?.Id, updates?.Id, StringComparison.Ordinal);
        var newlyUpdates = changed && updates is not null;
        (Updates, _duplicateOf) = (updates, duplicateOf);
        Note = !Readable ? Labels.PinSeveralUnreadable
            : duplicateOf is { } first ? Labels.PinSeveralSameName(first)
            : updates is not null ? Labels.PinSeveralUpdates(updates.Name)
            : string.Empty;
        if (!Readable || duplicateOf is not null || newlyUpdates)
        {
            _ticked = false;
        }

        OnChanged(nameof(Note));
        OnChanged(nameof(HasNote));
        OnChanged(nameof(CanTick));
        OnChanged(nameof(Ticked));
        OnChanged(nameof(IsWritten));
        OnChanged(nameof(AccessibleName));
        return changed;
    }
}
#pragma warning restore MA0182
