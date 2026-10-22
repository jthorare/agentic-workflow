using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>
/// Starts a line's phase as a linked instance (port; line spec LN-WF1, 'effect.line.start-phase'): the recipe the line pinned, at
/// '{line}/phase.{id}.{attempt}', with the phase's inputs. The address is the identity, so asking again for the same phase and attempt finds
/// the instance already started.
/// </summary>
public interface IPhaseStarts
{
    /// <summary>Starts the phase, or finds it started.</summary>
    /// <param name="start">What to start.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The linked instance and whether it already existed; an error when the line pinned no such recipe, or Temporal can't be reached.</returns>
    public ValueTask<Result<PhaseStarted>> Start(PhaseStart start, CancellationToken cancellationToken);
}
