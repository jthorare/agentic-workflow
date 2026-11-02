namespace Aw.Sindri.Features.GetRunSuspension;

/// <summary>The GetRunSuspension response.</summary>
/// <param name="Suspended">True while a park, escalation or takeover holds the run.</param>
public sealed record RunSuspensionView(bool Suspended);
