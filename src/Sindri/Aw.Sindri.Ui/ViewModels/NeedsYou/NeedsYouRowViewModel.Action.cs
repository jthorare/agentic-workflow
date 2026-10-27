namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>A row's action beyond its buttons (<see cref="NeedsYouRowViewModel"/>, continued): the note Home shows when an action could not be sent, and Complete on a row that is not a desk task.</summary>
internal sealed partial class NeedsYouRowViewModel
{
    /// <summary>Why the last action on this row sent nothing or was refused, with the command that does it; empty for none. Shown where <see cref="Notice"/> shows.</summary>
    internal string ActionNote
    {
        get;
        set
        {
            field = value;
            OnChanged(nameof(Notice));
            OnChanged(nameof(HasNotice));
        }
    } = "";

    // Complete reports an instance's desk run; elsewhere it is a command to run, not a button.
    private bool IsCliOnlyComplete(NeedsYouAction action) =>
        string.Equals(action.CatalogId, "CompleteStep", StringComparison.Ordinal) && !string.Equals(Entry.Subject?.Kind, "instance", StringComparison.Ordinal);
}
