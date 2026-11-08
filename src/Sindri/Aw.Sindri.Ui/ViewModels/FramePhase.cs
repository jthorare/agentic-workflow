namespace Aw.Sindri.Ui.ViewModels;

/// <summary>Where the frame's read stands.</summary>
internal enum FramePhase
{
    /// <summary>No instance named yet.</summary>
    Empty,

    /// <summary>Named, not yet answered (the skeleton).</summary>
    Loading,

    /// <summary>Read and shown.</summary>
    Shown,

    /// <summary>The API has no such instance.</summary>
    NotFound,

    /// <summary>The API's answer is in a shape this build can't read.</summary>
    Unreadable,

    /// <summary>Sindri refused the read, or could not answer it (a rebuild, a rotated token, a timeout): its code and detail are shown.</summary>
    Refused,
}
