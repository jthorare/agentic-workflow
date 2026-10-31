using System.Text.Json.Serialization;

namespace Aw.Context.Core.Eval.Backtest;

/// <summary>One sample's score on one arm.</summary>
/// <param name="IssueKey">The sample's issue.</param>
/// <param name="Arm">The arm's name.</param>
/// <param name="Unmapped">True when no truth file implements a node in the sample's as-of map: left out of the node metrics, kept in file recall.</param>
/// <param name="NodeRecall">Truth nodes found over truth nodes; null when unmapped.</param>
/// <param name="NodePrecision">Predicted nodes in the truth over predicted nodes (0 when none were predicted); null when unmapped.</param>
/// <param name="FileRecallAt20">Truth files in the first 20 predicted files over truth files.</param>
/// <param name="PredictedNodes">The nodes the answer named, ordered.</param>
/// <param name="TruthNodes">The nodes the truth files implement in the as-of map, ordered.</param>
/// <param name="PredictedFiles">The first 20 predicted files, in answer order.</param>
/// <param name="Leaky">True when an input of this sample/arm saw something after the cut.</param>
public sealed record SampleScore(string IssueKey, string Arm, bool Unmapped, double? NodeRecall, double? NodePrecision, double FileRecallAt20,
    IReadOnlyList<string> PredictedNodes, IReadOnlyList<string> TruthNodes, IReadOnlyList<string> PredictedFiles, bool Leaky)
{
    /// <summary>Precision with the adjudicator's labels; null when it did not run or stopped before this sample.</summary>
    public double? AdjudicatedPrecision { get; init; }

    /// <summary>Truth files in the first 10 predicted files over truth files.</summary>
    public double FileRecallAt10 { get; init; }

    /// <summary>One over the 1-based rank of the first truth file in the predicted files; 0 when none was predicted.</summary>
    public double ReciprocalRank { get; init; }

    /// <summary>The one pin the question's anchor sits in, so the pin leg ran for it; null for a question with no anchor pin or two. Set once per sample on the ranker arms.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Pin { get; init; }
}
