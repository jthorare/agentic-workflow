namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>What the Pin sheet was opened for.</summary>
internal enum PinSheetMode
{
    /// <summary>A pin that doesn't exist yet, empty.</summary>
    NewPin,

    /// <summary>A derived concept's card, pinned.</summary>
    PinThis,

    /// <summary>A pin of the profile, edited.</summary>
    EditPin,
}
