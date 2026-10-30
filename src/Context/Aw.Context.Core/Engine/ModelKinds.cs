using Aw.Context.Core.Models;

namespace Aw.Context.Core.Engine;

/// <summary>The profile names of the kinds, and which kind each purpose belongs to.</summary>
public static class ModelKinds
{
    /// <summary>Every kind, in the order the profile docs list them.</summary>
    public static IReadOnlyList<ModelKind> All { get; } = [ModelKind.Labeling, ModelKind.ConceptNaming, ModelKind.DecisionExtraction, ModelKind.Adjudication, ModelKind.MapCleanup, ModelKind.Rerank, ModelKind.ProductSurfaces];

    /// <summary>The kind a purpose is routed by: the concept scan, the concept names and the concept synthesis all use conceptNaming.</summary>
    /// <param name="purpose">The purpose.</param>
    /// <returns>The kind.</returns>
    public static ModelKind Of(ModelPurpose purpose) => purpose switch
    {
        ModelPurpose.Labeling => ModelKind.Labeling,
        ModelPurpose.Concepts or ModelPurpose.ConceptScan or ModelPurpose.ConceptSynthesis => ModelKind.ConceptNaming,
        ModelPurpose.DecisionExtraction => ModelKind.DecisionExtraction,
        ModelPurpose.Adjudication => ModelKind.Adjudication,
        ModelPurpose.Cleanup => ModelKind.MapCleanup,
        ModelPurpose.Rerank => ModelKind.Rerank,
        ModelPurpose.ProductSurfaces => ModelKind.ProductSurfaces,
        _ => throw new ArgumentOutOfRangeException(nameof(purpose)),
    };

    /// <summary>The kind a profile key names.</summary>
    /// <param name="key">The key as written.</param>
    /// <param name="kind">The kind, when the key is one.</param>
    /// <returns>Whether it is one.</returns>
    public static bool TryParse(string key, out ModelKind kind)
    {
        foreach (var candidate in All)
        {
            if (string.Equals(Key(candidate), key, StringComparison.Ordinal))
            {
                kind = candidate;
                return true;
            }
        }

        kind = default;
        return false;
    }

    /// <summary>The key the profile and the ledger use.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>'labeling', 'conceptNaming', 'decisionExtraction', 'adjudication', 'mapCleanup', 'rerank' or 'productSurfaces'.</returns>
    public static string Key(ModelKind kind) => kind switch
    {
        ModelKind.Labeling => "labeling",
        ModelKind.ConceptNaming => "conceptNaming",
        ModelKind.DecisionExtraction => "decisionExtraction",
        ModelKind.Adjudication => "adjudication",
        ModelKind.MapCleanup => "mapCleanup",
        ModelKind.Rerank => "rerank",
        ModelKind.ProductSurfaces => "productSurfaces",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
