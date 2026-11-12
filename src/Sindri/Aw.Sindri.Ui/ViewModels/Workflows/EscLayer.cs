namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>A layer Esc closes.</summary>
internal enum EscLayer
{
    /// <summary>Nothing is open.</summary>
    None,

    /// <summary>The Activate confirm.</summary>
    ActivateConfirm,

    /// <summary>A picker.</summary>
    Picker,

    /// <summary>A sheet.</summary>
    Sheet,

    /// <summary>The running test.</summary>
    RunningTest,
}
