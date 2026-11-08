namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>A row's action beyond its buttons (<see cref="NeedsYouRowViewModel"/>, continued): the note Home shows when an action could not be sent.</summary>
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
}
