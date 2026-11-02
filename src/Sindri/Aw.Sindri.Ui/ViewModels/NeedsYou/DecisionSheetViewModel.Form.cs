namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The decision sheet's form and its states (<see cref="DecisionSheetViewModel"/>, continued): the note, what Decide answered, the refusal, the cleared line and the live region.</summary>
internal sealed partial class DecisionSheetViewModel
{
    /// <summary>The note field's caption and name: it says the note is sent with the choice.</summary>
#pragma warning disable CA1822, MA0041 // a fixed sentence is a property because the view binds it
    internal string NoteLabel => Labels.DecisionNoteLabel;
#pragma warning restore CA1822, MA0041

    /// <summary>The note sent with the choice.</summary>
    internal string Note
    {
        get;
        set => Set(ref field, value);
    } = "";

    /// <summary>The served refusal's detail; empty for none.</summary>
    internal string Refusal { get; private set => Set(ref field, value); } = "";

    /// <summary>The served refusal's code; empty for none.</summary>
    internal string RefusalCode { get; private set => Set(ref field, value); } = "";

    /// <summary>What was decided, once Decide was accepted; empty before.</summary>
    internal string Outcome { get; private set => Set(ref field, value); } = "";

    /// <summary>True when the row left the list while the sheet was open.</summary>
    internal bool IsCleared { get; private set => Set(ref field, value); }

    /// <summary>The cleared line, once the row is gone.</summary>
    internal string ClearedLine => !IsCleared || DecidedLine.Length > 0 || IsRefusedAsDecided ? "" : ServedCleared.Length > 0 ? ServedCleared : Labels.DecisionClearedFallback;

    // A stale Decide's refusal already says the decision was answered; the cleared line would say it a second time.
    private bool IsRefusedAsDecided { get; set; }

    /// <summary>The polite live region: set true before the text, spoken once.</summary>
    internal bool IsAnnouncing { get; private set => Set(ref field, value); }

    /// <summary>The live region's text.</summary>
    internal string Announcement { get; private set => Set(ref field, value); } = Labels.Sindri;
}
