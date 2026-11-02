using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Evaluation;

/// <summary>How a run ends, as the decision row records it.</summary>
internal sealed record Settlement
{
    /// <summary>The outcome.</summary>
    public required DecisionOutcome Outcome { get; init; }

    /// <summary>The decision, or null for an escalation.</summary>
    public string? Decision { get; init; }

    /// <summary>The confidence (0 for an escalation).</summary>
    public double Confidence { get; init; }

    /// <summary>Who settled it, or null for an escalation (the row's provider is then 'none').</summary>
    public ProviderName? Provider { get; init; }

    /// <summary>The provider column: the wire name, or 'none'.</summary>
    public string ProviderColumn => Provider is { } name ? ProviderNames.Name(name) : EvaluationReasons.NoProvider;

    /// <summary>The reason code.</summary>
    public required string ReasonCode { get; init; }

    /// <summary>The settling provider's latency.</summary>
    public long LatencyMs { get; init; }

    /// <summary>The settling provider's chain position (the skipped count for an escalation).</summary>
    public int ChainPosition { get; init; }

    /// <summary>The probabilities only the deciding answer sent; the envelope carries these and never an earlier provider's.</summary>
    public IReadOnlyDictionary<string, double>? DecidingProbabilities { get; init; }

    /// <summary>The probabilities to keep in the details (the deciding answer's, else an earlier below-threshold answer's: the row may keep the merged value).</summary>
    public IReadOnlyDictionary<string, double>? Probabilities { get; init; }

    /// <summary>The deciding answer's "reasons" (a JSON array), as the provider sent them; stored redacted and capped.</summary>
    public System.Text.Json.Nodes.JsonNode? Reasons { get; init; }

    /// <summary>What the fallback rules would have said.</summary>
    public string? RulesOpinion { get; init; }

    /// <summary>How that opinion compared.</summary>
    public Agreement? Agreement { get; init; }
}
