using Aw.Kernel;

namespace Aw.Sindri.Features.OnyxUp;

/// <summary>One recorded step of `onyx up`: it knows the stamp of its inputs and, given what state.json recorded, checks that its result still stands and repairs it when it doesn't.</summary>
internal interface IOnyxUpStep
{
    /// <summary>Gets the step's name as state.json records it.</summary>
    public string Name { get; }

    /// <summary>Hashes the step's inputs and the stamp of the step before it.</summary>
    /// <param name="context">The run's shared facts.</param>
    /// <param name="upstream">The stamp of the step before this one in the plan; null for the first step. The plan computes the chain, so a step never names its upstream.</param>
    /// <returns>The stamp.</returns>
    public string Stamp(OnyxUpContext context, string? upstream);

    /// <summary>Makes the step's result hold: does nothing when the recorded stamp is the current one and what the step made is still as it should be, and does the work otherwise.</summary>
    /// <param name="context">The run's shared facts.</param>
    /// <param name="stamp">The step's current stamp (see <see cref="Stamp"/>).</param>
    /// <param name="recorded">The stamp state.json holds for the step, or null.</param>
    /// <param name="cancellationToken">Cancels the step.</param>
    /// <returns>What it did, or the error that stops `onyx up` (nothing is recorded for the step).</returns>
    public ValueTask<Result<OnyxStepOutcome>> Execute(OnyxUpContext context, string stamp, string? recorded, CancellationToken cancellationToken);
}
