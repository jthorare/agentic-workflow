using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>One launch of a scoping state's agent Step, as a contract test sets it up.</summary>
/// <param name="Run">The scoping instance.</param>
/// <param name="State">The recipe state.</param>
/// <param name="Step">The state's Step id.</param>
/// <param name="Requested">The model the state's 'with.model' asks for; null when it asks for none.</param>
/// <param name="Number">The run's number.</param>
/// <param name="Launch">The launch.</param>
public sealed record ScopeLaunch(InstanceAddress Run, string State, string Step, string? Requested, int Number, LaunchId Launch);
