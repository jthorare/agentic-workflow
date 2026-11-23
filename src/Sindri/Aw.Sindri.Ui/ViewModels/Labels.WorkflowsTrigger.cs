namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): how a workflow run started, in words (#3654).</summary>
internal static partial class Labels
{
    /// <summary>What started a run.</summary>
    /// <param name="kind">The trigger's kind, as served.</param>
    /// <returns>The words.</returns>
    internal static string WfStartedBy(string? kind) => kind switch
    {
        "manual" => "Started by hand",
        "schedule" => "Started on a schedule",
        "workflow_call" => "Started by another workflow",
        "connection_event" => "Started by an event from a connection",
        null or "" => "Started by " + WfUnknownTrigger,
        _ => "Started by a " + kind + " trigger",
    };

    /// <summary>A run row's trigger when the object has no kind.</summary>
    internal const string WfUnknownTrigger = "an unknown trigger";
}
