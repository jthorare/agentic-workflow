namespace Aw.Sindri.Tests.Workflows;

/// <summary>The run-store methods <see cref="FaultingRunStore"/> can fail.</summary>
internal enum RunOp
{
    /// <summary>Reading a run.</summary>
    Get,

    /// <summary>Writing a run's status.</summary>
    SetStatus,

    /// <summary>Writing a node run.</summary>
    RecordNodeRun,
}
