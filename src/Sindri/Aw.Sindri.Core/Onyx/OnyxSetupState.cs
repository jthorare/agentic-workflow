namespace Aw.Sindri.Core.Onyx;

/// <summary>What `sindri onyx up` has done, written to onyx/state.json after each step succeeds.</summary>
/// <param name="Port">The loopback port setup chose and keeps across reruns.</param>
/// <param name="Steps">Each completed step (see <see cref="OnyxSteps"/>) with the stamp of the inputs it ran with; a step whose stamp still matches is skipped.</param>
/// <param name="Project">The compose project name `onyx up` started the stack under; diagnostic only: it detects a state file copied from another home. It is never used to choose the compose project (status, doctor, down and up act on this home's own name and warn about a recorded name that differs), and must not be trusted if multi-project support is ever added (null in a state written before it was recorded).</param>
/// <param name="SchemaVersion">The layout of this file (1); a file of another version is read as unreadable, never half-understood. A future schema bump needs a migration from v1 that carries `Port` and `Project` over; without one, every rerun would lose the chosen port and the recorded project.</param>
public sealed record OnyxSetupState(int Port, IReadOnlyDictionary<string, string> Steps, string? Project = null, int SchemaVersion = OnyxSetupState.CurrentSchema)
{
    /// <summary>The schema version this build writes and reads.</summary>
    public const int CurrentSchema = 1;
}
