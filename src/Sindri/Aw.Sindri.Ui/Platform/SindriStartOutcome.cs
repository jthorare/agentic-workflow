namespace Aw.Sindri.Ui.Platform;

/// <summary>What a try at starting Sindri did.</summary>
public enum SindriStartOutcome
{
    /// <summary>The program was started.</summary>
    Started,

    /// <summary>The app already started it this run.</summary>
    AlreadyStarted,

    /// <summary>No 'sindri' sits beside the app: a broken install.</summary>
    NotInstalled,
}
