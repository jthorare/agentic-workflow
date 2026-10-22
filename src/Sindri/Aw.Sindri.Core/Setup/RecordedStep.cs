namespace Aw.Sindri.Core.Setup;

/// <summary>The last 'setup.step.changed' recorded for a step.</summary>
/// <param name="State">The state it moved to.</param>
/// <param name="Code">The error code it failed with, or null.</param>
/// <param name="Count">How many change events the step has in all.</param>
public sealed record RecordedStep(SetupState State, string? Code, int Count);
