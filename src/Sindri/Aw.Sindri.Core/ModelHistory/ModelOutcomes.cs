namespace Aw.Sindri.Core.ModelHistory;

/// <summary>What a failed Step run counts as for the model that ran it.</summary>
public static class ModelOutcomes
{
    private const string InvalidResult = "invalid-result:";

    /// <summary>
    /// The outcome of a 'step.failed'. The kind the engine recorded ('refused', 'check_failed' or 'crashed') is used as it is. Without one (a row written before the engine
    /// recorded it, or a kind this doesn't know) the reason decides: it starts with 'invalid-result:' for a refused report; 'exit', 'lost', 'idle' and 'launch' are the
    /// session's end, not the model; anything else failed the Step's check.
    /// </summary>
    /// <param name="failureKind">The kind the row records; null when it records none.</param>
    /// <param name="reason">The failure's reason; null reads as a failed check.</param>
    /// <returns>The outcome.</returns>
    public static ModelOutcome OfFailure(string? failureKind, string? reason) => failureKind switch
    {
        "refused" => ModelOutcome.Refused,
        "check_failed" => ModelOutcome.CheckFailed,
        "crashed" => ModelOutcome.Crashed,
        _ => OfReason(reason),
    };

    private static ModelOutcome OfReason(string? reason) => reason switch
    {
        null => ModelOutcome.CheckFailed,
        _ when reason.StartsWith(InvalidResult, StringComparison.Ordinal) => ModelOutcome.Refused,
        "exit" or "lost" or "idle" or "launch" => ModelOutcome.Crashed,
        _ => ModelOutcome.CheckFailed,
    };
}
