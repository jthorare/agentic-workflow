namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>Which of the Not running page's states shows (first-run spec §4.1).</summary>
internal enum StartStage
{
    /// <summary>Not running, nothing started: the page with Start Sindri.</summary>
    Idle,

    /// <summary>The app started Sindri and waits for it.</summary>
    Starting,

    /// <summary>Nothing answered 30 s after the start.</summary>
    DidntStart,

    /// <summary>A second serve was refused: one is already starting.</summary>
    AlreadyStarting,

    /// <summary>No 'sindri' beside the app.</summary>
    NotInstalled,
}
