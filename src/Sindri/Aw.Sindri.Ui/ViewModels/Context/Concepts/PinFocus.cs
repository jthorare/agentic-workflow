namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>Which pin button takes focus when the Pin sheet closes: the one that opened it.</summary>
internal enum PinFocus
{
    /// <summary>The layer head's New pin.</summary>
    NewPin,

    /// <summary>The layer head's Add several.</summary>
    AddSeveral,

    /// <summary>Pin this on a card.</summary>
    PinThis,

    /// <summary>Edit pin on a card.</summary>
    EditPin,
}
