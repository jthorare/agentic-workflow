namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// What a block may reach (spec §2). A block is capability-bearing when this isn't <see cref="None"/>, when a
/// parameter is a capability field, or (in a generated recipe) when it starts a child. Serialized as a number.
/// </summary>
[Flags]
public enum Capability
{
    /// <summary>Nothing beyond its inputs.</summary>
    None = 0,

    /// <summary>Runs tools.</summary>
    Tools = 1,

    /// <summary>Emits effects.</summary>
    Effects = 2,

    /// <summary>Sets an autonomy tier.</summary>
    AutonomyTier = 4,

    /// <summary>Sets a write scope.</summary>
    WriteScope = 8,

    /// <summary>Uses the network.</summary>
    Network = 16,

    /// <summary>Spends a budget.</summary>
    Budget = 32,

    /// <summary>Runs as the holdout evaluator: its own scratch checkout, the phase's hidden set read-only, the evidence CLIs (LN-LCH2).</summary>
    Evaluator = 64,
}
