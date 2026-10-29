namespace Aw.Sindri.Ui.ViewModels.Sandbox;

/// <summary>What the Health row and the Settings section say about the sandbox (sandbox spec §8 States).</summary>
internal enum SandboxHealthState
{
    /// <summary>Nothing has been checked yet.</summary>
    NeverChecked,

    /// <summary>The last check found no blocker.</summary>
    Ready,

    /// <summary>The last check found a blocker.</summary>
    Blocked,

    /// <summary>A network learn window is open (a warning).</summary>
    Learning,

    /// <summary>Sindri answered with something this build can't read.</summary>
    Unreadable,

    /// <summary>Sindri doesn't answer.</summary>
    ServeDown,
}
