namespace Aw.Judge.Core.Decisions;

/// <summary>How a decision ended: stored as 'decided', 'escalated' or 'failed'.</summary>
public enum DecisionOutcome
{
    /// <summary>A provider (or a rule) decided.</summary>
    Decided = 1,

    /// <summary>Nothing decided and no provider failed (a disabled question, invalid input, below threshold).</summary>
    Escalated = 2,

    /// <summary>Nothing decided and at least one provider failed.</summary>
    Failed = 3,
}
