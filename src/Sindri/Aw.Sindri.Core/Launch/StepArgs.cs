namespace Aw.Sindri.Core.Launch;

/// <summary>
/// What an unattended Step adds to a launch (launcher L-17). Present only when the launch is unattended, so the argv builder
/// reads it structurally, with no flag. A requested model or policy whose catalog form is 'none' is SND-LAUNCH-013 and never launches.
/// </summary>
/// <param name="Model">The model id, or null for the CLI's default.</param>
/// <param name="Policy">The Step's policy, or null for none.</param>
/// <param name="Ref">The Step whose seed prompt the session gets.</param>
public sealed record StepArgs(string? Model, StepPolicy? Policy, StepRef Ref);
