namespace Aw.Sindri.Core.Setup;

/// <summary>A change to record: <paramref name="Count"/> is the step's earlier change events, the n of the key.</summary>
/// <param name="Id">The step.</param>
/// <param name="State">The state it moved to.</param>
/// <param name="Code">The error code when it failed, else null.</param>
/// <param name="Count">The step's earlier change events.</param>
public sealed record StepChange(SetupStepId Id, SetupState State, string? Code, int Count);
