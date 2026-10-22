namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>The linked instance <see cref="IPhaseStarts.Start"/> started or found.</summary>
/// <param name="Address">Its address, '{line}/phase.{id}.{attempt}'.</param>
/// <param name="Existed">It was already started: nothing was started now.</param>
public sealed record PhaseStarted(InstanceAddress Address, bool Existed);
