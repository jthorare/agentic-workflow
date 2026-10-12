using Aw.Judge.Core.Ids;

namespace Aw.Judge.Core.Decisions;

/// <summary>One judge decision (the 'decisions' table), with its detail row when it has one.</summary>
public sealed record DecisionRecord
{
    /// <summary>The decision id.</summary>
    public required DecisionId Id { get; init; }

    /// <summary>When it was decided.</summary>
    public required DateTimeOffset Ts { get; init; }

    /// <summary>The question name, e.g. 'wake-gate'.</summary>
    public required string Question { get; init; }

    /// <summary>The content class of the input.</summary>
    public required string ContentClass { get; init; }

    /// <summary>The provider that settled it, or 'none'.</summary>
    public required string Provider { get; init; }

    /// <summary>The decision, or null when nothing decided.</summary>
    public string? Decision { get; init; }

    /// <summary>The confidence, 0 to 1.</summary>
    public required double Confidence { get; init; }

    /// <summary>Why, e.g. 'pre-rule' or 'below-threshold'.</summary>
    public required string ReasonCode { get; init; }

    /// <summary>How long the decision took, in milliseconds.</summary>
    public required long LatencyMs { get; init; }

    /// <summary>The first 16 hex characters of the SHA-256 of the input's canonical JSON.</summary>
    public required string InputDigest { get; init; }

    /// <summary>When the decision was undone, if it was.</summary>
    public DateTimeOffset? UndoneAt { get; init; }

    /// <summary>When the user approved the decision ('judge approve'), if they did; the first approval's time.</summary>
    public DateTimeOffset? ApprovedAt { get; init; }

    /// <summary>The settling provider's position in the chain.</summary>
    public required int ChainPosition { get; init; }

    /// <summary>The providers passed over before it, in chain order.</summary>
    public required IReadOnlyList<SkippedProvider> Skipped { get; init; }

    /// <summary>How it ended.</summary>
    public required DecisionOutcome Outcome { get; init; }

    /// <summary>The detail row, or null when it was never written or has been pruned.</summary>
    public DecisionDetails? Details { get; init; }
}
