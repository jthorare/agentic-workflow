namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Workflows list's column heads and who a run waits on (F3 workflows spec §6). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The Item column's head.</summary>
    internal const string WorkflowsColItem = "Item";

    /// <summary>The Stage column's head.</summary>
    internal const string WorkflowsColStage = "Stage";

    /// <summary>The Status column's head.</summary>
    internal const string WorkflowsColStatus = "Status";

    /// <summary>The Step column's head.</summary>
    internal const string WorkflowsColStep = "Step";

    /// <summary>The Waiting on column's head.</summary>
    internal const string WorkflowsColWaitingOn = "Waiting on";

    /// <summary>The Updated column's head.</summary>
    internal const string WorkflowsColUpdated = "Updated";

    /// <summary>A run that waits on the user.</summary>
    internal const string WorkflowsWaitYou = "You";

    /// <summary>A run an agent is working on.</summary>
    internal const string WorkflowsWaitAgent = "AI agent";
}
