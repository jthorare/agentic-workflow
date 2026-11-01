namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>A word typed by hand (<see cref="PinSheetViewModel"/>, continued): it gets the same checks as the filled ones, inline.</summary>
internal sealed partial class PinSheetViewModel
{
    /// <summary>Adds the typed word as a row: ticked, or unticked with the reason it can't be a word of this pin.</summary>
    internal void AddWord()
    {
        var word = NewWord.Trim();
        if (word.Length == 0)
        {
            return;
        }

        NewWord = string.Empty;
        var form = PinWordRules.Normalized(word);
        if (form.Length > 0 && Words.Any(w => string.Equals(PinWordRules.Normalized(w.Word), form, StringComparison.Ordinal)))
        {
            return;
        }

        Words.Add(Watched(Checked(word, typed: true)));
        Edited();
    }
}
