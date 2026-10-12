using System.Text.Json.Nodes;

namespace Aw.Judge.Core.Providers;

/// <summary>The provider answered.</summary>
/// <param name="Decision">The decision (checked against the question's outputs by the engine).</param>
/// <param name="Confidence">The confidence, 0 to 1.</param>
/// <param name="ReasonCode">Why.</param>
public sealed record ProviderDecided(string Decision, double Confidence, string ReasonCode) : ProviderResult(ReasonCode)
{
    /// <summary>Response fields beyond the decision (chosenIndex, reasons), passed through to the caller.</summary>
    public JsonObject? Extra { get; init; }

    /// <summary>The provider's per-output probabilities, when it gives them.</summary>
    public IReadOnlyDictionary<string, double>? Probabilities { get; init; }
}
