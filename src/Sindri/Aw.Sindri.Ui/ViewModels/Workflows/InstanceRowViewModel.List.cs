namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The words a list row shows in its columns (<see cref="InstanceRowViewModel"/>, continued; F3 workflows spec §2 "List").</summary>
internal sealed partial class InstanceRowViewModel
{
    /// <summary>The Stage column: the board lane the run sits in, else the state it is in, made readable.</summary>
    public string StageName => LineStages.LaneOf(_row.LineStage) is { } lane ? LineStages.Name(lane) : WorkflowNames.Of(_row.State, displayName: null);

    /// <summary>The mono reference under the title: the run's address.</summary>
    public string Reference => _row.Address;

    /// <summary>The Waiting on column: you (it needs you, or you took it over), the AI agent (it is working), nobody once it ended or has not started.</summary>
    public string WaitingOn => Status.Kind switch
    {
        RunStatusKind.WaitingForYou or RunStatusKind.Paused or RunStatusKind.TookOver => Labels.WorkflowsWaitYou,
        RunStatusKind.Running => Labels.WorkflowsWaitAgent,
        RunStatusKind.Unknown or RunStatusKind.NotStarted or RunStatusKind.Done or RunStatusKind.Failed or RunStatusKind.DidntWorkOut or RunStatusKind.Cancelled => string.Empty,
        _ => string.Empty,
    };

    /// <summary>The Updated column: how long since the run last changed state ("2h 5m").</summary>
    public string UpdatedText => Labels.Age(Age);
}
