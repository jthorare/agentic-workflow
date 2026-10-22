namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// Where a run of the line workflow sits on the board (F3 workflows spec §4 "Lane of a card"). The line has five states and no merge
/// state, so Merge stays empty until the API serves a line stage; Build and prove takes the phase state meanwhile. A stopgap table: it
/// goes when the engine serves stage names.
/// </summary>
internal static class LineStages
{
    /// <summary>The board's lanes, in order.</summary>
    internal static IReadOnlyList<LaneId> Lanes { get; } = [LaneId.Scoping, LaneId.Design, LaneId.Plan, LaneId.Build, LaneId.Merge, LaneId.Done];

    /// <summary>The lane an open run is in.</summary>
    /// <param name="lineStage">The run's served line stage: the state id of its root line instance.</param>
    /// <returns>The lane, or null when the run has no line stage or it is finished.</returns>
    internal static LaneId? LaneOf(string? lineStage) => lineStage switch
    {
        "scoping" => LaneId.Scoping,
        "design" or "redesign" => LaneId.Design,
        "plan" => LaneId.Plan,
        "phase" => LaneId.Build,
        _ => null,
    };

    /// <summary>A lane's name.</summary>
    /// <param name="lane">The lane.</param>
    /// <returns>The words.</returns>
    internal static string Name(LaneId lane) => lane switch
    {
        LaneId.Scoping => Labels.WorkflowsLaneScoping,
        LaneId.Design => Labels.WorkflowsLaneDesign,
        LaneId.Plan => Labels.WorkflowsLanePlan,
        LaneId.Build => Labels.WorkflowsLaneBuild,
        LaneId.Merge => Labels.WorkflowsLaneMerge,
        LaneId.Done => Labels.WorkflowsLaneDone,
        _ => string.Empty,
    };

    /// <summary>What an empty lane teaches: its own sentence for Merge and Done, the plain one for the rest.</summary>
    /// <param name="lane">The lane.</param>
    /// <returns>The words under "Nothing here."</returns>
    internal static string EmptyBody(LaneId lane) => lane switch
    {
        LaneId.Merge => Labels.WorkflowsLaneMergeEmpty,
        LaneId.Done => Labels.WorkflowsLaneDoneEmpty,
        LaneId.Scoping or LaneId.Design or LaneId.Plan or LaneId.Build => Labels.WorkflowsLaneEmptyBody,
        _ => string.Empty,
    };
}
