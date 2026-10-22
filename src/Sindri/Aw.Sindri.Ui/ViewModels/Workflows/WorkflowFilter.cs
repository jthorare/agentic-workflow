namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>Which runs the Workflows page shows (F3 workflows spec §3).</summary>
internal enum WorkflowFilter
{
    /// <summary>Every run.</summary>
    All,

    /// <summary>Runs waiting for the user: a decision, an escalation, or a pause.</summary>
    NeedsYou,

    /// <summary>Runs that need the user, and runs the user took over (the owner field is not served yet).</summary>
    Mine,
}
