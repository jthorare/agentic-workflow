namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>The state a card, a step, a runs-list row or the header badge is in (F3 spec 3 §3): the one table of words, glyphs and brushes.</summary>
internal enum RunNodeState
{
    /// <summary>Not started: an empty ring.</summary>
    NotStarted,

    /// <summary>Running: a spinner.</summary>
    Running,

    /// <summary>Done: a check.</summary>
    Done,

    /// <summary>Failed: a cross in the fail tone.</summary>
    Failed,

    /// <summary>Didn't work out: a minus in a circle, in the attention tone.</summary>
    DidntWorkOut,

    /// <summary>Cancelled: a muted cross.</summary>
    Cancelled,

    /// <summary>Waiting for you: a clock; a decision is open.</summary>
    Waiting,

    /// <summary>Paused, needs you: a clock; the run is parked or escalated.</summary>
    Paused,

    /// <summary>You took over: a person.</summary>
    TookOver,
}
