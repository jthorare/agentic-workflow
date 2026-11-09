namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>Where Settings' Obsidian todo note card stands (todo spec §3.4).</summary>
internal enum TodoNoteCardStage
{
    /// <summary>Nothing is read yet.</summary>
    Reading,

    /// <summary>The library has no vault: the card says to add one.</summary>
    NoVaults,

    /// <summary>The vault and note fields, with Check note.</summary>
    Form,

    /// <summary>A check is shown with the buttons that follow from it.</summary>
    Checked,

    /// <summary>A note is set: its line with Change and Stop syncing.</summary>
    Syncing,
}
