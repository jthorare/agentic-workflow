using Aw.Judge.Core.Decisions;

namespace Aw.Judge.Infrastructure.Sqlite;

/// <summary>The stored text of the decision enums ('decided', 'overrode'); SkipReason is stored through its JSON names.</summary>
internal static class DecisionText
{
    internal static string Of(DecisionOutcome outcome) => outcome switch
    {
        DecisionOutcome.Decided => "decided",
        DecisionOutcome.Escalated => "escalated",
        DecisionOutcome.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Not a decision outcome."),
    };

    internal static DecisionOutcome Outcome(string text) => text switch
    {
        "decided" => DecisionOutcome.Decided,
        "escalated" => DecisionOutcome.Escalated,
        "failed" => DecisionOutcome.Failed,
        _ => throw new InvalidDataException($"Stored outcome '{text}' is not decided, escalated or failed."),
    };

    internal static string Of(Agreement agreement) => agreement switch
    {
        Agreement.Agreed => "agreed",
        Agreement.Overrode => "overrode",
        Agreement.Undecided => "undecided",
        _ => throw new ArgumentOutOfRangeException(nameof(agreement), agreement, "Not an agreement."),
    };

    internal static Agreement AgreementOf(string text) => text switch
    {
        "agreed" => Agreement.Agreed,
        "overrode" => Agreement.Overrode,
        "undecided" => Agreement.Undecided,
        _ => throw new InvalidDataException($"Stored agreement '{text}' is not agreed, overrode or undecided."),
    };
}
