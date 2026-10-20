namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The inbox's groups, in the order the page lists them: the four kinds of request, then the failures, then whatever the app does not know.</summary>
internal enum NeedsYouGroupKind
{
    /// <summary>Your approvals: the desk tasks.</summary>
    Approvals,

    /// <summary>Paused runs: a run that waits for an answer, and a run you took over.</summary>
    Runs,

    /// <summary>Agent decisions.</summary>
    Decisions,

    /// <summary>Workflow and settings approvals.</summary>
    Settings,

    /// <summary>Something failed (a service).</summary>
    Failures,

    /// <summary>A row of a kind the app does not know, headed by the served kind.</summary>
    Other,
}
