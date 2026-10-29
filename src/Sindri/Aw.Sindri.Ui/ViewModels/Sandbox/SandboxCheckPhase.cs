namespace Aw.Sindri.Ui.ViewModels.Sandbox;

/// <summary>Where Check again stands (sandbox spec §8 States, Check again).</summary>
internal enum SandboxCheckPhase
{
    /// <summary>Not pressed, or the last outcome was dismissed.</summary>
    Idle,

    /// <summary>A check is running; the label stays, a spinner shows, a second press does nothing.</summary>
    InFlight,

    /// <summary>The check found blockers; the card lists every one.</summary>
    StillFailing,

    /// <summary>All clear; the Steps that would resume are listed before anything is sent.</summary>
    AllClear,

    /// <summary>Every listed Step resumed.</summary>
    Resumed,

    /// <summary>Some listed Steps moved on; the rest stay parked.</summary>
    ResumedPartial,
}
