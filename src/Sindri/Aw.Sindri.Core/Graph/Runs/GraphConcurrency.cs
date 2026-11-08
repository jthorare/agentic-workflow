namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>How the runs of one workflow overlap (the spec's 'concurrency').</summary>
public enum GraphConcurrency
{
    /// <summary>A trigger that fires while a run is in flight is skipped and logged. The default.</summary>
    OnePerWorkflow = 1,

    /// <summary>A trigger that fires while a run is in flight is recorded as 'queued' and waits for its turn.</summary>
    Queue,

    /// <summary>Every trigger starts a run.</summary>
    Unlimited,
}
