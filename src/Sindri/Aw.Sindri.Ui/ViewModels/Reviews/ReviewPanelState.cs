namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One panel's state (review page spec §6.3).</summary>
internal enum ReviewPanelState
{
    /// <summary>A one-line placeholder in the panel's place.</summary>
    Loading,

    /// <summary>Rows.</summary>
    Ready,

    /// <summary>The empty words.</summary>
    Empty,

    /// <summary>The error words.</summary>
    Error,
}
