namespace Aw.Sindri.Ui.Desktop;

/// <summary>What a try at starting Sindri did.</summary>
internal enum StartOutcome
{
    /// <summary>The program was started.</summary>
    Started,

    /// <summary>The app already started it: it is not started a second time.</summary>
    AlreadyStarted,

    /// <summary>No 'sindri' sits beside the app: a broken install, not a user step.</summary>
    NotInstalled,
}
