using System.Text.Json;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>What a Step input fills from (spec §2). The context engine's phase adds what retrieval needs.</summary>
/// <param name="Input">The Step input's id.</param>
/// <param name="Step">The Step it fills for.</param>
/// <param name="Arguments">Its 'with:' arguments, resolved to values.</param>
/// <param name="Facts">The instance's facts.</param>
public sealed record StepInputContext(
    BlockId Input,
    BlockId Step,
    IReadOnlyDictionary<string, JsonElement> Arguments,
    IReadOnlyDictionary<FactKey, JsonElement> Facts);
