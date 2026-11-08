namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>What a trigger became.</summary>
public enum GraphStartKind
{
    /// <summary>A run was recorded and its workflow started.</summary>
    Started = 1,

    /// <summary>A run was recorded as 'queued' and waits for the one in flight.</summary>
    Queued,

    /// <summary>'one-per-workflow' and a run is in flight: nothing was recorded; the skip was logged.</summary>
    Skipped,
}
