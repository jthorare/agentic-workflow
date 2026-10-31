namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>One of the words people use on the Pin sheet: a tick, the word and the line that counts its records or says why it is not ticked.</summary>
internal sealed class PinWordRow : ObservableObject
{
    internal PinWordRow(string word, int? newRecords, bool ticked, string? note = null, bool counting = false, bool typed = false)
    {
        (Word, NewRecords, Note, IsCounting, IsTyped) = (word, newRecords, note, counting, typed);
        Ticked = ticked;
    }

    /// <summary>The word.</summary>
    public string Word { get; }

    /// <summary>How many stored records the alias rule would newly link to the pin; null while counting, and for a word typed by hand.</summary>
    public int? NewRecords { get; }

    /// <summary>Why the word is not ticked ("Already a word of the pin Auth (\"login\")", "About 1,240 more records, likely too broad"), or null.</summary>
    public string? Note { get; }

    /// <summary>True while its records are still counted.</summary>
    public bool IsCounting { get; }

    /// <summary>True for a word the person typed.</summary>
    public bool IsTyped { get; }

    /// <summary>True when the word was refused (another pin's word, or not a word): it can't be ticked.</summary>
    public bool IsRefused { get; internal init; }

    /// <summary>Whether it goes into the pin's aliases.</summary>
    public bool Ticked
    {
        get;
        set
        {
            if (value && IsRefused)
            {
                return;
            }

            if (field != value)
            {
                field = value;
                OnChanged(nameof(Ticked));
                TickChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>The first line: the word, with its count when it has one and nothing to say against it ("payments · about 41 more records").</summary>
    public string Text => Note is null && NewRecords is { } n ? Word + " · " + Labels.PinSheetMoreRecords(n) : Word;

    /// <summary>The second line: why the word isn't ticked, or "Counting records…"; empty otherwise.</summary>
    public string Detail => Note ?? (IsCounting ? Labels.PinSheetCounting : string.Empty);

    /// <summary>True when the second line has words.</summary>
    public bool HasDetail => Detail.Length > 0;

    /// <summary>The screen reader's name: the word and its line.</summary>
    public string AccessibleName => HasDetail ? Word + ". " + Detail : Text;

    /// <summary>The tick changed: the sheet counts it as an edit.</summary>
    internal event EventHandler? TickChanged;
}
