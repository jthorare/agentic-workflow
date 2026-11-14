namespace Aw.Sindri.Ui.ViewModels.Rows;

/// <summary>The colour meaning of a chip fact. The adapter sets it from the API's fact kind (state, ci); the view never reads it out of the text.</summary>
internal enum RowTone
{
    /// <summary>No status: plain chip.</summary>
    Neutral,

    /// <summary>Good: done, merged, CI green, on track.</summary>
    Success,

    /// <summary>Needs a look: blocked, parked, at risk, changes asked.</summary>
    Attention,

    /// <summary>Bad: failed, CI red, off track.</summary>
    Danger,

    /// <summary>Work in progress now.</summary>
    Running,

    /// <summary>Waiting on someone: in review.</summary>
    Waiting,

    /// <summary>Not ready: draft.</summary>
    Draft,
}
