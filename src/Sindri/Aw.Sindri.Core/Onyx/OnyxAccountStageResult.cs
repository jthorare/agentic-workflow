namespace Aw.Sindri.Core.Onyx;

/// <summary>What the account stage ended with. Nothing here is a secret: the key is a pointer to where it is kept.</summary>
/// <param name="Url">Where Onyx answers.</param>
/// <param name="KeyPointer">The pointer to the stored key (never the key); null while the key step waits for the human.</param>
/// <param name="ProfileChanged">The profile file was written in this run, so it needs the human's approval.</param>
/// <param name="Steps">What each step did: signup, key, profile.</param>
public sealed record OnyxAccountStageResult(string Url, string? KeyPointer, bool ProfileChanged, IReadOnlyList<OnyxStepResult> Steps)
{
    /// <summary>Gets a value indicating whether a step is waiting for the human.</summary>
    public bool NeedsHuman => Steps.Any(static s => s.Outcome == OnyxStepOutcome.NeedsHuman);
}
