using System.Text.Json;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>What an evidence provider collects (spec §2, A §17.11). The engine (P3) fills it; later phases may add members.</summary>
/// <param name="Provider">The provider's id.</param>
/// <param name="Kind">The evidence kind the transition names.</param>
/// <param name="At">Base or head.</param>
/// <param name="Facts">The instance's facts.</param>
/// <param name="Address">The instance's address, for a provider that keeps state in the instance's run area (null when the engine gave none).</param>
public sealed record EvidenceContext(
    BlockId Provider,
    string Kind,
    EvidencePoint At,
    IReadOnlyDictionary<FactKey, JsonElement> Facts,
    string? Address = null);
