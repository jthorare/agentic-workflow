using System.Text.Json;
using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>'context.models' (model routing spec §3): which harnesses and models each kind of call uses. A missing key is its default.</summary>
public sealed record ModelsSettings
{
    /// <summary>The first 'candidates' value when the profile writes none.</summary>
    public const int DefaultCandidates = 3;

    /// <summary>'names': what cheap and strong mean per harness, including the old 'context.conceptScan.models' folded in when 'names' has no such key.</summary>
    public ModelNames Names { get; init; } = ModelNames.None;

    /// <summary>'default': what every kind inherits, key by key.</summary>
    public ModelEntry Default { get; init; } = new();

    /// <summary>The kinds the profile wrote an entry for.</summary>
    public IReadOnlyDictionary<ModelKind, ModelEntry> Kinds { get; init; } = new Dictionary<ModelKind, ModelEntry>();

    /// <summary>'labeling.pick'.</summary>
    public LabelPick LabelPick { get; init; } = LabelPick.Off;

    /// <summary>'labeling.candidates': how many candidate labels the model writes per cluster (2 to 5).</summary>
    public int LabelCandidates { get; init; } = DefaultCandidates;

    /// <summary>What the profile check should print as warnings; today only the old 'context.conceptScan.models' written beside 'context.models.names'.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>The entry the profile wrote for a kind, or an empty one.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The entry.</returns>
    public ModelEntry Entry(ModelKind kind) => Kinds.TryGetValue(kind, out var entry) ? entry : new ModelEntry();

    /// <summary>Reads and checks a 'context.models' value; a missing or null one is the defaults.</summary>
    /// <param name="section">The value as written.</param>
    /// <returns>The settings, or SND-CONTEXT-001 naming the key path.</returns>
    public static Result<ModelsSettings> Read(JsonElement? section) => Read(section, legacy: null);

    internal static Result<ModelsSettings> Read(JsonElement? section, ConceptScanModelsSection? legacy)
    {
        _ = legacy;
        return section is not { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } ? new ModelsSettings() : throw new NotSupportedException();
    }
}
