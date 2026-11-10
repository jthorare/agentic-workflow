namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The words of the Workflows page's empty state (<see cref="Labels"/>, continued).</summary>
internal static partial class Labels
{
    /// <summary>The empty state's create action.</summary>
    internal const string WfNew = "New workflow";

    /// <summary>Why the create action is off: the app has no create call yet.</summary>
    internal const string WfNewOff = "Making a workflow from the app isn't built yet.";

    /// <summary>The line before the agent command.</summary>
    internal const string WfAgentRoute = "Agents can make one with";

    /// <summary>The command an agent runs to make a workflow.</summary>
    internal const string WfAgentCommand = "sindri wf create";
}
