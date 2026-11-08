namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>What the bar approves on a row whose primary is the approval the app does.</summary>
internal enum NeedsYouConfirmKind
{
    /// <summary>The row's primary is not the approval.</summary>
    None,

    /// <summary>A drifted profile: one click sends its hash.</summary>
    Profile,
}
