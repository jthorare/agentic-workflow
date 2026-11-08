namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>What the bar approves on a row whose primary is one of the two approvals the app does.</summary>
internal enum NeedsYouConfirmKind
{
    /// <summary>The row's primary is not one of the two.</summary>
    None,

    /// <summary>A changed workflow: one click sends its approval.</summary>
    Recipe,

    /// <summary>Approve all new workflows: one click on the plan the approve-all dialog's own view model read.</summary>
    All,
}
