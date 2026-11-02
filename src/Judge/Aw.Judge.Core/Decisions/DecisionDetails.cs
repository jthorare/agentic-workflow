using Aw.Judge.Core.Ids;

namespace Aw.Judge.Core.Decisions;

/// <summary>The detail row kept beside a decision (the 'decision_details' table); pruned after <see cref="Retention"/>.</summary>
public sealed record DecisionDetails
{
    /// <summary>How long a detail row is kept: a decision older than this keeps its row in 'decisions' but loses its details.</summary>
    public static TimeSpan Retention { get; } = TimeSpan.FromDays(30);

    /// <summary>The redacted, capped input as JSON.</summary>
    public required string InputJson { get; init; }

    /// <summary>The model's reasons (redacted, capped), or null when none were recorded: a row written before they were kept, or an answer that carried none.</summary>
    public IReadOnlyList<string>? Reasons { get; init; }

    /// <summary>The provider's per-option probabilities, when it gave them.</summary>
    public IReadOnlyDictionary<string, double>? Probabilities { get; init; }

    /// <summary>What the fallback rules would have said, when they had an opinion.</summary>
    public string? RulesOpinion { get; init; }

    /// <summary>How the rules' opinion compared with the decision.</summary>
    public Agreement? Agreement { get; init; }

    /// <summary>The session the question came from, when known.</summary>
    public SessionId? SessionId { get; init; }
}
