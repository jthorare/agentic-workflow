namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>What the catalog says of an action.</summary>
internal enum ActionReadiness
{
    /// <summary>The app runs it.</summary>
    Ready,

    /// <summary>The catalog has not listed it (or has not been read yet): shown disabled with its command.</summary>
    Pending,
}
